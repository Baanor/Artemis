﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Artemis.Managers;
using Artemis.Models.Interfaces;
using Artemis.Profiles.Layers.Abstract;
using Artemis.Profiles.Layers.Animations;
using Artemis.Profiles.Layers.Interfaces;
using Artemis.Profiles.Layers.Models;
using Artemis.Profiles.Layers.Types.Generic;
using Artemis.Properties;
using Artemis.Utilities;
using Artemis.Utilities.Keyboard;

namespace Artemis.Profiles.Layers.Types.KeyPress
{
    internal class KeyPressType : ILayerType
    {
        private readonly MainManager _mainManager;
        private List<LayerModel> _keyPressLayers = new List<LayerModel>();
        private LayerPropertiesModel _properties;

        public KeyPressType(MainManager mainManager)
        {
            _mainManager = mainManager;
            KeyboardHook.KeyDownCallback += KeyboardHookOnKeyDownCallback;
        }

        public RadialGradientBrush TempBrush { get; set; }


        public string Name { get; } = "Keyboard - Key press";
        public bool ShowInEdtor { get; } = false;
        public DrawType DrawType { get; } = DrawType.Keyboard;

        public ImageSource DrawThumbnail(LayerModel layer)
        {
            var thumbnailRect = new Rect(0, 0, 18, 18);
            var visual = new DrawingVisual();
            using (var c = visual.RenderOpen())
                c.DrawImage(ImageUtilities.BitmapToBitmapImage(Resources.gif), thumbnailRect);

            var image = new DrawingImage(visual.Drawing);
            return image;
        }

        public void Draw(LayerModel layer, DrawingContext c)
        {
            lock (_keyPressLayers)
            {
                foreach (var keyPressLayer in _keyPressLayers)
                    keyPressLayer.LayerType.Draw(keyPressLayer, c);
            }
        }

        public void Update(LayerModel layerModel, IDataModel dataModel, bool isPreview = false)
        {
            // Key press is always as large as the entire keyboard it is drawn for
            layerModel.Properties.Width = _mainManager.DeviceManager.ActiveKeyboard.Width;
            layerModel.Properties.Height = _mainManager.DeviceManager.ActiveKeyboard.Height;
            layerModel.Properties.X = 0;
            layerModel.Properties.Y = 0;
            layerModel.Properties.Contain = true;

            _properties = layerModel.Properties;

            lock (_keyPressLayers)
            {
                // Remove expired key presses
                _keyPressLayers = _keyPressLayers.Where(k => !k.LayerAnimation.MustExpire(k)).ToList();
                // Update the ones that are still active
                foreach (var keyPressLayer in _keyPressLayers)
                    keyPressLayer.Update(null, false, true);
            }
        }

        public void SetupProperties(LayerModel layerModel)
        {
            if (layerModel.Properties is SimplePropertiesModel)
                return;

            layerModel.Properties = new SimplePropertiesModel(layerModel.Properties);
        }

        public LayerPropertiesViewModel SetupViewModel(LayerPropertiesViewModel layerPropertiesViewModel,
            List<ILayerAnimation> layerAnimations, IDataModel dataModel, LayerModel proposedLayer)
        {
            if (layerPropertiesViewModel is GenericPropertiesViewModel)
                return layerPropertiesViewModel;
            return new GenericPropertiesViewModel(proposedLayer, dataModel, layerAnimations);
        }

        private void KeyboardHookOnKeyDownCallback(KeyEventArgs e)
        {
            if (_properties == null)
                return;

            var keyMatch = _mainManager.DeviceManager.ActiveKeyboard.GetKeyPosition(e.KeyCode);
            if (keyMatch == null)
                return;

            lock (_keyPressLayers)
            {
                var layer = LayerModel.CreateLayer();
                layer.Properties.Brush = _properties.Brush.CloneCurrentValue();
                layer.Properties.X = keyMatch.Value.X - 3;
                layer.Properties.Y = keyMatch.Value.Y - 3;
                layer.Properties.Width = 6;
                layer.Properties.Height = 6;

                layer.Properties.AnimationSpeed = 1;
                layer.LayerAnimation = new GrowAnimation();

                _keyPressLayers.Add(layer);
            }
        }
    }
}