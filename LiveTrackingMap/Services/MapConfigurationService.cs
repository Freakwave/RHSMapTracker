using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Nts;
using Mapsui.Tiling;
using Mapsui.Styles.Thematics;

namespace LiveTrackingMap.Services
{
    public static class MapConfigurationService
    {
        public static Map CreateDefaultMap()
        {
            var map = new Map();
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            return map;
        }

        public static StyleCollection CreateEditLayerStyle()
        {
            return new StyleCollection
            {
                Styles = {
                    CreateEditLayerBasicStyle(),
                    CreateSelectedStyle()
                }
            };
        }

        private static IStyle CreateEditLayerBasicStyle()
        {
            var editStyle = new VectorStyle
            {
                Fill = new Brush(new Color(255, 0, 0, 180)),
                Line = new Pen(new Color(255, 0, 0, 180), 3),
                Outline = new Pen(new Color(255, 0, 0, 180), 3)
            };
            return editStyle;
        }

        private static IStyle CreateSelectedStyle()
        {
            return new ThemeStyle(f => (bool?)f["Selected"] == true ? 
                new SymbolStyle
                {
                    Fill = null,
                    Outline = new Pen(Color.Red, 3),
                    Line = new Pen(Color.Red, 3)
                } : 
                new SymbolStyle { Enabled = false });
        }
    }
}