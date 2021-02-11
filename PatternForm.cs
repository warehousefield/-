using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using RvtView = Autodesk.Revit.DB.View;

namespace PatternForm
{
    public partial class  PatternForm : System.Windows.Forms.Form
    {
        UIDocument docUI;       
        Document doc;
      
        public PatternForm(ExternalCommandData commandData)
        {
            docUI = commandData.Application.ActiveUIDocument;
            doc = commandData.Application.ActiveUIDocument.Document;
            InitializeComponent();
            IniTreeView();
        }
       
        private List<T> GetAllElements<T>()
        {
            ElementClassFilter elementFilter = new ElementClassFilter(typeof(T));
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.WherePasses(elementFilter);
            return collector.Cast<T>().ToList();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    
        private void IniTreeView()
        {

            this.treeViewFillPattern.Nodes.Clear();
            TreeNode iniNode1 = new TreeNode("FillPatterns");
            treeViewFillPattern.Nodes.Add(iniNode1);

            List<FillPatternElement> lstFillPatterns = GetAllElements<FillPatternElement>();
            for (int i = 0; i < lstFillPatterns.Count; i++)
            {
                TreeNode node = new TreeNode(lstFillPatterns[i].Name);
                node.Name = i.ToString();
                iniNode1.Nodes.Add(node);
            }
        }

        private FillPatternElement GetOrCreateFacePattern(string patternName)
        {
            FillPatternTarget target = FillPatternTarget.Model;
            FillPatternElement fillPatternElement = FillPatternElement.GetFillPatternElementByName(doc, target, patternName);

            if (fillPatternElement == null)
            {
                //Create a fillpattern with specified angle and spacing
                FillPattern fillPattern = new FillPattern(patternName, target,
                    FillPatternHostOrientation.ToView, 0.5, 0.5, 0.5);

                Transaction trans = new Transaction(doc);
                trans.Start("Create a fillpattern element");
                fillPatternElement = FillPatternElement.Create(doc, fillPattern);
                trans.Commit();
            }
            return fillPatternElement;
        }

        private FillPatternElement GetOrCreateComplexFacePattern(string patternName)
        {
            FillPatternTarget target = FillPatternTarget.Model;
            FillPatternElement fillPatternElement = FillPatternElement.GetFillPatternElementByName(doc, target, patternName);

            if (fillPatternElement == null)
            {               
                FillPattern fillPattern = new FillPattern(patternName, target,
                                                          FillPatternHostOrientation.ToHost);

                List<FillGrid> grids = new List<FillGrid>();
  
                grids.Add(CreateGrid(new UV(0, 0.1), 0.5, 0, 0.55, 1.0, 0.1));
                grids.Add(CreateGrid(new UV(0, 0.5), 0.5, 0, 0.55, 1.0, 0.1));

                grids.Add(CreateGrid(new UV(0, 0.1), 0.55, Math.PI / 2, 0.5, 0.4, 0.6));
                grids.Add(CreateGrid(new UV(1.0, 0.1), 0.55, Math.PI / 2, 0.5, 0.4, 0.6));

                fillPattern.SetFillGrids(grids);

                Transaction t = new Transaction(doc, "Create fill pattern");
                t.Start();
                fillPatternElement = FillPatternElement.Create(doc, fillPattern);

                t.Commit();
            }

            return fillPatternElement;
        }

        private FillGrid CreateGrid(UV origin, double offset, double angle,
                                    double shift, params double[] segments)
        {
            FillGrid fillGrid = new FillGrid();
           
            fillGrid.Origin = origin;
            fillGrid.Offset = offset;
            fillGrid.Angle = angle;
            fillGrid.Shift = shift;
            List<double> segmentsList = new List<double>();
            foreach (double d in segments)
            {
                segmentsList.Add(d);
            }
            fillGrid.SetSegments(segmentsList);

            return fillGrid;
        }

        private void buttonCreateFillPattern_Click(object sender, EventArgs e)
        {
            Wall targetWall = GetSelectedWall();
            if (targetWall == null)
            { 
                this.Close();
                return;
            }

            FillPatternElement mySurfacePattern = GetOrCreateFacePattern("MySurfacePattern");
            Material targetMaterial = doc.GetElement(targetWall.GetMaterialIds(false).First<ElementId>()) as Material;
            Transaction trans = new Transaction(doc);
            trans.Start("Apply fillpattern to surface");
            targetMaterial.SurfacePatternId = mySurfacePattern.Id;
            trans.Commit();
            this.Close();
        }

        private void SetParameter(string paramName, ElementId eid, Element elem)
        {
            foreach (Parameter param in elem.Parameters)
            {
                if (param.Definition.Name == paramName)
                {
                    Transaction trans = new Transaction(doc);
                    trans.Start("Set parameter value");
                    param.Set(eid);
                    trans.Commit();
                    break;
                }
            }
        }
        
        private void buttonApplyToSurface_Click(object sender, EventArgs e)
        {
            foreach (ElementId elemId in docUI.Selection.GetElementIds())
            {
                Element targetWall = doc.GetElement(elemId);
                if (targetWall == null)
                {
                    this.Close();
                    return;
                }


                if (treeViewFillPattern.SelectedNode == null || treeViewFillPattern.SelectedNode.Parent == null)
                {                   
                    return;
                }

                List<FillPatternElement> lstPatterns = GetAllElements<FillPatternElement>();
                int patternIndex = int.Parse(treeViewFillPattern.SelectedNode.Name);
                Material targetMaterial = doc.GetElement(targetWall.GetMaterialIds(false).First<ElementId>()) as Material;
                Transaction trans = new Transaction(doc);
                trans.Start("Apply fillpattern to surface");
                targetMaterial.SurfacePatternId = lstPatterns[patternIndex].Id;
                trans.Commit();

                this.Close();
            }
        }

        private Wall GetSelectedWall()
        {
            Wall wall = null;
            foreach (ElementId elemId in docUI.Selection.GetElementIds())
            {
                Element elem = doc.GetElement(elemId);
                wall = elem as Wall;
                if (wall != null)
                    return wall;
            }
            return wall;
        }

        private void buttonApplyToGrids_Click(object sender, EventArgs e)
        {
            List<ElementId> lstGridTypeIds = new List<ElementId>();
            GetSelectedGridTypeIds(lstGridTypeIds);
            if (lstGridTypeIds.Count == 0)
            {
                TaskDialog.Show("Apply To Grids",
                    "Before applying a LinePattern to Grids, you must first select at least one grid.");
                this.Close();
                return;
            }

            if (treeViewLinePattern.SelectedNode == null || treeViewLinePattern.Parent == null)
            {  
                return;
            }
            ElementId eid = new ElementId(int.Parse(treeViewLinePattern.SelectedNode.Name));
            foreach (ElementId typeId in lstGridTypeIds)
            {
                Element gridType = doc.GetElement(typeId);
                SetParameter("End Segment Pattern", eid, gridType);
            }
            this.Close();
        }
       
        private void GetSelectedGridTypeIds(List<ElementId> lstGridTypeIds)
        {
            foreach (ElementId elemId in docUI.Selection.GetElementIds())
            {
                Element elem = doc.GetElement(elemId);
                Grid grid = elem as Grid;
                if (grid != null)
                {
                    ElementId gridTypeId = grid.GetTypeId();
                    if (!lstGridTypeIds.Contains(gridTypeId))
                        lstGridTypeIds.Add(gridTypeId);
                }
            }
        }      
               
                private void buttonCreateComplexFillPattern_Click(object sender, EventArgs e)
                {
                    Wall targetWall = GetSelectedWall();
                    if (targetWall == null)
                    {
                        TaskDialog.Show("Create Fill Pattern",
                            "Before applying a FillPattern to a wall's surfaces, you must first select a wall.");
                        this.Close();
                        return;
                    }

                    FillPatternElement mySurfacePattern = GetOrCreateComplexFacePattern("MyComplexPattern");
                    Material targetMaterial = doc.GetElement(targetWall.GetMaterialIds(false).First<ElementId>()) as Material;
                    Transaction trans = new Transaction(doc);
                    trans.Start("Apply complex fillpattern to surface");
                    targetMaterial.SurfacePatternId = mySurfacePattern.Id;
                    trans.Commit();
                    this.Close();
                }

        private void PatternForm_Load(object sender, EventArgs e)
        {

        }
    }
}
