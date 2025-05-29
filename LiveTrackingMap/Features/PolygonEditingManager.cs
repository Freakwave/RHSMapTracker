using Mapsui.UI;
using Mapsui.Nts.Editing;
using Mapsui.Layers;
using Mapsui.Nts.Widgets;
using Mapsui.Extensions;

namespace LiveTrackingMap.Features
{
    public class PolygonEditingManager
    {
        private readonly WritableLayer _editablePolygonLayer;
        private EditManager? _editManager;
        private EditManipulation? _editManipulation;
        
        public bool IsDrawingPolygon { get; private set; }
        
        public PolygonEditingManager(WritableLayer editablePolygonLayer)
        {
            _editablePolygonLayer = editablePolygonLayer;
        }

        public void InitializeEditing(IMapControl mapControl)
        {
            _editManipulation = new EditManipulation();
            _editManager = new EditManager
            {
                Layer = _editablePolygonLayer
            };

            var editingWidget = new EditingWidget(mapControl, _editManager, _editManipulation);
            mapControl.Map.Widgets.Add(editingWidget);
        }

        public void TogglePolygonEdit()
        {
            if (_editManager == null) return;

            if (_editManager.EditMode != EditMode.None)
            {
                _editManager.EndEdit();
                _editManager.EditMode = EditMode.None;
                IsDrawingPolygon = false;
            }
            else
            {
                _editManager.EditMode = EditMode.AddPolygon;
                IsDrawingPolygon = true;
            }
        }
    }
}