﻿using System;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Artemis.DAL;
using Artemis.Events;
using Artemis.KeyboardProviders;
using Artemis.Managers;
using Artemis.Models;
using Artemis.Models.Profiles;
using Artemis.Models.Profiles.Properties;
using Artemis.Services;
using Artemis.Utilities;
using Artemis.ViewModels.LayerEditor;
using Caliburn.Micro;
using MahApps.Metro;
using Ninject;

namespace Artemis.ViewModels
{
    public sealed class ProfileEditorViewModel : Screen, IHandle<ActiveKeyboardChanged>
    {
        private readonly GameModel _gameModel;
        private readonly MainManager _mainManager;
        private DateTime _downTime;
        private LayerModel _draggingLayer;
        private Point? _draggingLayerOffset;
        private LayerEditorViewModel _editorVm;
        private ImageSource _keyboardPreview;
        private Cursor _keyboardPreviewCursor;
        private BindableCollection<LayerModel> _layers;
        private BindableCollection<ProfileModel> _profiles;
        private bool _resizing;
        private LayerModel _selectedLayer;
        private ProfileModel _selectedProfile;

        public ProfileEditorViewModel(IEventAggregator events, MainManager mainManager, GameModel gameModel)
        {
            _mainManager = mainManager;
            _gameModel = gameModel;

            Profiles = new BindableCollection<ProfileModel>();
            Layers = new BindableCollection<LayerModel>();
            ActiveKeyboard = _mainManager.KeyboardManager.ActiveKeyboard;

            events.Subscribe(this);

            PreviewTimer = new Timer(40);
            PreviewTimer.Elapsed += InvokeUpdateKeyboardPreview;


            PropertyChanged += PropertyChangeHandler;
            LoadProfiles();
        }

        [Inject]
        public MetroDialogService DialogService { get; set; }

        public Timer PreviewTimer { get; set; }

        public BindableCollection<ProfileModel> Profiles
        {
            get { return _profiles; }
            set
            {
                if (Equals(value, _profiles)) return;
                _profiles = value;
                NotifyOfPropertyChange(() => Profiles);
            }
        }

        public BindableCollection<LayerModel> Layers
        {
            get { return _layers; }
            set
            {
                if (Equals(value, _layers)) return;
                _layers = value;
                NotifyOfPropertyChange(() => Layers);
            }
        }

        public LayerModel SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                if (Equals(value, _selectedLayer)) return;
                _selectedLayer = value;
                NotifyOfPropertyChange(() => SelectedLayer);
                NotifyOfPropertyChange(() => CanRemoveLayer);
            }
        }

        public Cursor KeyboardPreviewCursor
        {
            get { return _keyboardPreviewCursor; }
            set
            {
                if (Equals(value, _keyboardPreviewCursor)) return;
                _keyboardPreviewCursor = value;
                NotifyOfPropertyChange(() => KeyboardPreviewCursor);
            }
        }

        public ProfileModel SelectedProfile
        {
            get { return _selectedProfile; }
            set
            {
                if (Equals(value, _selectedProfile)) return;
                _selectedProfile = value;

                Layers.Clear();
                if (_selectedProfile != null)
                    Layers.AddRange(_selectedProfile.Layers);

                NotifyOfPropertyChange(() => SelectedProfile);
                NotifyOfPropertyChange(() => CanAddLayer);
                NotifyOfPropertyChange(() => CanRemoveLayer);
            }
        }

        public ImageSource KeyboardPreview
        {
            get { return _keyboardPreview; }
            set
            {
                if (Equals(value, _keyboardPreview)) return;
                _keyboardPreview = value;
                NotifyOfPropertyChange(() => KeyboardPreview);
            }
        }

        public ImageSource KeyboardImage => ImageUtilities.BitmapToBitmapImage(ActiveKeyboard?.PreviewSettings.Image);

        public PreviewSettings? PreviewSettings => ActiveKeyboard?.PreviewSettings;

        public bool CanAddLayer => _selectedProfile != null;
        public bool CanRemoveLayer => _selectedProfile != null && _selectedLayer != null;

        private KeyboardProvider ActiveKeyboard { get; set; }

        /// <summary>
        ///     Handles chaning the active keyboard, updating the preview image and profiles collection
        /// </summary>
        /// <param name="message"></param>
        public void Handle(ActiveKeyboardChanged message)
        {
            ActiveKeyboard = _mainManager.KeyboardManager.ActiveKeyboard;
            NotifyOfPropertyChange(() => KeyboardImage);
            NotifyOfPropertyChange(() => PreviewSettings);
            LoadProfiles();
        }

        /// <summary>
        ///     Handles refreshing the layer preview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PropertyChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "KeyboardPreview")
                return;

            if (SelectedProfile != null)
                ProfileProvider.AddOrUpdate(SelectedProfile);
        }

        /// <summary>
        ///     Loads all profiles for the current game and keyboard
        /// </summary>
        private void LoadProfiles()
        {
            Profiles.Clear();
            if (_gameModel == null || ActiveKeyboard == null)
                return;

            Profiles.AddRange(ProfileProvider.GetAll(_gameModel, ActiveKeyboard));
            SelectedProfile = Profiles.FirstOrDefault();
        }

        /// <summary>
        ///     Adds a new profile to the current game and keyboard
        /// </summary>
        public async void AddProfile()
        {
            var name = await DialogService.ShowInputDialog("Add new profile",
                "Please provide a profile name unique to this game and keyboard.");

            // Null when the user cancelled
            if (name == null)
                return;

            if (name.Length < 1)
            {
                DialogService.ShowMessageBox("Invalid profile name", "Please provide a valid profile name");
                return;
            }

            var profile = new ProfileModel
            {
                Name = name,
                KeyboardName = ActiveKeyboard.Name,
                GameName = _gameModel.Name
            };

            if (ProfileProvider.GetAll().Contains(profile))
            {
                var overwrite = await DialogService.ShowQuestionMessageBox("Overwrite existing profile",
                    "A profile with this name already exists for this game. Would you like to overwrite it?");
                if (!overwrite.Value)
                    return;
            }

            ProfileProvider.AddOrUpdate(profile);

            LoadProfiles();
            SelectedProfile = profile;
        }

        /// <summary>
        ///     Opens a new LayerEditorView for the given layer
        /// </summary>
        /// <param name="layer">The layer to open the view for</param>
        public void LayerEditor(LayerModel layer)
        {
            IWindowManager manager = new WindowManager();
            _editorVm = new LayerEditorViewModel(_gameModel.GameDataModel, layer);
            dynamic settings = new ExpandoObject();

            settings.Title = "Artemis | Edit " + layer.Name;
            manager.ShowDialog(_editorVm, null, settings);

            // Refresh the layer list and reselect the last layer
            NotifyOfPropertyChange(() => Layers);
            SelectedLayer = layer;
        }

        /// <summary>
        ///     Adds a new layer to the profile and selects it
        /// </summary>
        public void AddLayer()
        {
            if (_selectedProfile == null)
                return;

            var layer = SelectedProfile.AddLayer();
            Layers.Add(layer);

            SelectedLayer = layer;
        }

        /// <summary>
        ///     Removes the currently selected layer from the profile
        /// </summary>
        public void RemoveLayer()
        {
            if (_selectedProfile == null || _selectedLayer == null)
                return;

            SelectedProfile.Layers.Remove(_selectedLayer);
            Layers.Remove(_selectedLayer);

            SelectedProfile.FixOrder();
        }

        /// <summary>
        ///     Removes the given layer from the profile
        /// </summary>
        /// <param name="layer"></param>
        public void RemoveLayerFromMenu(LayerModel layer)
        {
            SelectedProfile.Layers.Remove(layer);
            Layers.Remove(layer);

            SelectedProfile.FixOrder();
        }

        /// <summary>
        ///     Clones the given layer and adds it to the profile, on top of the original
        /// </summary>
        /// <param name="layer"></param>
        public void CloneLayer(LayerModel layer)
        {
            var clone = GeneralHelpers.Clone(layer);
            clone.Order = layer.Order - 1;
            SelectedProfile.Layers.Add(clone);
            Layers.Add(clone);

            SelectedProfile.FixOrder();
        }

        /// <summary>
        ///     Moves the currently selected layer up in the profile's layer tree
        /// </summary>
        public void LayerUp()
        {
            if (SelectedLayer == null)
                return;

            var reorderLayer = SelectedLayer;

            if (SelectedLayer.Parent != null)
                SelectedLayer.Parent.Reorder(SelectedLayer, true);
            else
                SelectedLayer.Profile.Reorder(SelectedLayer, true);

            NotifyOfPropertyChange(() => Layers);
            SelectedLayer = reorderLayer;
        }

        /// <summary>
        ///     Moves the currently selected layer down in the profile's layer tree
        /// </summary>
        public void LayerDown()
        {
            if (SelectedLayer == null)
                return;

            var reorderLayer = SelectedLayer;

            if (SelectedLayer.Parent != null)
                SelectedLayer.Parent.Reorder(SelectedLayer, false);
            else
                SelectedLayer.Profile.Reorder(SelectedLayer, false);

            NotifyOfPropertyChange(() => Layers);
            SelectedLayer = reorderLayer;
        }

        /// <summary>
        ///     Handler for clicking
        /// </summary>
        /// <param name="e"></param>
        public void MouseDownKeyboardPreview(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                _downTime = DateTime.Now;
        }

        /// <summary>
        ///     Second handler for clicking, selects a the layer the user clicked on
        ///     if the used clicked on an empty spot, deselects the current layer
        /// </summary>
        /// <param name="e"></param>
        public void MouseUpKeyboardPreview(MouseButtonEventArgs e)
        {
            var timeSinceDown = DateTime.Now - _downTime;
            if (!(timeSinceDown.TotalMilliseconds < 500))
                return;

            var pos = e.GetPosition((Image) e.OriginalSource);
            var x = pos.X/((double) ActiveKeyboard.PreviewSettings.Width/ActiveKeyboard.Width);
            var y = pos.Y/((double) ActiveKeyboard.PreviewSettings.Height/ActiveKeyboard.Height);

            var hoverLayer = SelectedProfile.Layers
                .OrderBy(l => l.Order)
                .Where(l => l.MustDraw())
                .FirstOrDefault(l => ((KeyboardPropertiesModel) l.Properties)
                    .GetRect(1)
                    .Contains(x, y));

            SelectedLayer = hoverLayer;
        }

        /// <summary>
        ///     Handler for resizing and moving the currently selected layer
        /// </summary>
        /// <param name="e"></param>
        public void MouseMoveKeyboardPreview(MouseEventArgs e)
        {
            var pos = e.GetPosition((Image) e.OriginalSource);
            var x = pos.X/((double) ActiveKeyboard.PreviewSettings.Width/ActiveKeyboard.Width);
            var y = pos.Y/((double) ActiveKeyboard.PreviewSettings.Height/ActiveKeyboard.Height);
            var hoverLayer = SelectedProfile.Layers.OrderBy(l => l.Order).Where(l => l.MustDraw())
                .FirstOrDefault(l => ((KeyboardPropertiesModel) l.Properties).GetRect(1).Contains(x, y));

            HandleDragging(e, x, y, hoverLayer);

            if (hoverLayer == null)
            {
                KeyboardPreviewCursor = Cursors.Arrow;
                return;
            }


            // Turn the mouse pointer into a hand if hovering over an active layer
            if (hoverLayer == SelectedLayer)
            {
                var rect = ((KeyboardPropertiesModel) hoverLayer.Properties).GetRect(1);
                KeyboardPreviewCursor =
                    Math.Sqrt(Math.Pow(x - rect.BottomRight.X, 2) + Math.Pow(y - rect.BottomRight.Y, 2)) < 0.6
                        ? Cursors.SizeNWSE
                        : Cursors.SizeAll;
            }
            else
                KeyboardPreviewCursor = Cursors.Hand;
        }

        private void InvokeUpdateKeyboardPreview(object sender, ElapsedEventArgs e)
        {
                Application.Current.Dispatcher.Invoke(UpdateKeyboardPreview, DispatcherPriority.ContextIdle);
        }

        /// <summary>
        ///     Generates a new image for the keyboard preview
        /// </summary>
        public void UpdateKeyboardPreview()
        {
            if (_selectedProfile == null || ActiveKeyboard == null)
                return;
            
            var keyboardRect = ActiveKeyboard.KeyboardRectangle(4);
            var visual = new DrawingVisual();
            using (var drawingContext = visual.RenderOpen())
            {
                // Setup the DrawingVisual's size
                drawingContext.PushClip(new RectangleGeometry(keyboardRect));
                drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), null, keyboardRect);

                // Draw the layers
                foreach (var layer in _selectedProfile.Layers.OrderByDescending(l => l.Order).Where(l => l.MustDraw()))
                    layer.Draw<object>(null, drawingContext, true, false);

                // Get the selection color
                var color = (Color) ThemeManager.DetectAppStyle(Application.Current).Item2.Resources["AccentColor"];
                var pen = new Pen(new SolidColorBrush(color), 0.4);

                // Draw the selection outline and resize indicator
                if (SelectedLayer != null && SelectedLayer.MustDraw())
                {
                    var layerRect = ((KeyboardPropertiesModel) SelectedLayer.Properties).GetRect();
                    // Deflate the rect so that the border is drawn on the inside
                    layerRect.Inflate(-0.2, -0.2);

                    // Draw an outline around the selected layer
                    drawingContext.DrawRectangle(null, pen, layerRect);
                    // Draw a resize indicator in the bottom-right
                    drawingContext.DrawLine(pen,
                        new Point(layerRect.BottomRight.X - 1, layerRect.BottomRight.Y - 0.5),
                        new Point(layerRect.BottomRight.X - 1.2, layerRect.BottomRight.Y - 0.7));
                    drawingContext.DrawLine(pen,
                        new Point(layerRect.BottomRight.X - 0.5, layerRect.BottomRight.Y - 1),
                        new Point(layerRect.BottomRight.X - 0.7, layerRect.BottomRight.Y - 1.2));
                    drawingContext.DrawLine(pen,
                        new Point(layerRect.BottomRight.X - 0.5, layerRect.BottomRight.Y - 0.5),
                        new Point(layerRect.BottomRight.X - 0.7, layerRect.BottomRight.Y - 0.7));
                }

                // Remove the clip
                drawingContext.Pop();
            }
            KeyboardPreview = new DrawingImage(visual.Drawing);
        }

        /// <summary>
        ///     Handles dragging the given layer
        /// </summary>
        /// <param name="e"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="hoverLayer"></param>
        private void HandleDragging(MouseEventArgs e, double x, double y, LayerModel hoverLayer)
        {
            // Reset the dragging state on mouse release
            if (e.LeftButton == MouseButtonState.Released ||
                (_draggingLayer != null && _selectedLayer != _draggingLayer))
            {
                _draggingLayerOffset = null;
                _draggingLayer = null;
                return;
            }

            if (SelectedLayer == null)
                return;

            // Setup the dragging state on mouse press
            if (_draggingLayerOffset == null && hoverLayer != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var layerRect = ((KeyboardPropertiesModel) hoverLayer.Properties).GetRect(1);
                var selectedProps = (KeyboardPropertiesModel) SelectedLayer.Properties;

                _draggingLayerOffset = new Point(x - selectedProps.X, y - selectedProps.Y);
                _draggingLayer = hoverLayer;
                if (Math.Sqrt(Math.Pow(x - layerRect.BottomRight.X, 2) + Math.Pow(y - layerRect.BottomRight.Y, 2)) < 0.6)
                    _resizing = true;
                else
                    _resizing = false;
            }

            if (_draggingLayerOffset == null || _draggingLayer == null || (_draggingLayer != SelectedLayer))
                return;

            var draggingProps = (KeyboardPropertiesModel) _draggingLayer?.Properties;

            // If no setup or reset was done, handle the actual dragging action
            if (_resizing)
            {
                draggingProps.Width = (int) Math.Round(x - draggingProps.X);
                draggingProps.Height = (int) Math.Round(y - draggingProps.Y);
                if (draggingProps.Width < 1)
                    draggingProps.Width = 1;
                if (draggingProps.Height < 1)
                    draggingProps.Height = 1;
            }
            else
            {
                draggingProps.X = (int) Math.Round(x - _draggingLayerOffset.Value.X);
                draggingProps.Y = (int) Math.Round(y - _draggingLayerOffset.Value.Y);
            }
        }
    }
}