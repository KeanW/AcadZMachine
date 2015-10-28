using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ZLR.VM;

namespace AcadZMachine
{
  public static class Extensions
  {
    public static bool? GetYesOrNo(
      this Editor ed, string prompt, bool yesDefault = true
    )
    {
      var pko = new PromptKeywordOptions(prompt + " [Yes/No]: ", "Yes No");
      pko.Keywords.Default = yesDefault ? "Yes" : "No";
      var pr = ed.GetKeywords(pko);
      if (pr.Status != PromptStatus.OK)
        return null;
      return (pr.StringResult == "Yes");
    }

    public static string GetFilename(
      this Editor ed, string prompt, bool openFile = true
    )
    {
      PromptFileNameResult pfnr;
      if (openFile)
      {
        var pofo = new PromptOpenFileOptions(prompt);
        pfnr = ed.GetFileNameForOpen(pofo);
      }
      else
      {
        var psfo = new PromptSaveFileOptions(prompt);
        pfnr = ed.GetFileNameForSave(psfo);
      }

      if (pfnr.Status != PromptStatus.OK)
        return null;

      return pfnr.StringResult;
    }

    public static Point3d MidPoint(this Curve c)
    {
      return c.StartPoint + (c.EndPoint - c.StartPoint) * 0.5;
    }
  }

  public class Commands
  {
    [DllImport(
      "AcJsCoreStub.crx", CharSet = CharSet.Auto,
      CallingConvention = CallingConvention.Cdecl,
      EntryPoint = "acjsInvokeAsync")]
    extern static private int acjsInvokeAsync(
      string name, string jsonArgs
    );

    internal class Node
    {
      public string Name { get; set; }
      public Point3d Position { get; set; }
    }

    internal class Edge
    {
      public string Name { get; set; }
      public Point3d Start { get; set; }
      public Point3d End { get; set; }
    }

    private PaletteSet _isops = null;

    [CommandMethod("ZM")]
    public void ZMachine()
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var ed = doc.Editor;

      var pofo = new PromptOpenFileOptions("File to load");
      var pfnr = ed.GetFileNameForOpen(pofo);
      if (pfnr.Status == PromptStatus.OK)
        ExecuteGame(pfnr.StringResult, ed);
    }

    [CommandMethod("ZORK")]
    public void Zork()
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var ed = doc.Editor;

      var asm = Assembly.GetExecutingAssembly();
      ExecuteGame(Path.GetDirectoryName(asm.Location) + "\\zork1.z3", ed);
    }

    [CommandMethod("ZORK2")]
    public void Zork2()
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var ed = doc.Editor;

      var asm = Assembly.GetExecutingAssembly();
      ExecuteGame(Path.GetDirectoryName(asm.Location) + "\\zork2.z3", ed);
    }

    [CommandMethod("ZORK3")]
    public void Zork3()
    {
      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return;
      var ed = doc.Editor;

      var asm = Assembly.GetExecutingAssembly();
      ExecuteGame(Path.GetDirectoryName(asm.Location) + "\\zork3.z3", ed);
    }

    void ExecuteGame(string gameFile, Editor ed)
    {
      var showScore = ed.GetYesOrNo("Show the score as we go?", false);
      if (!showScore.HasValue) return;

      var saveCmds = ed.GetYesOrNo("Save commands to file?", false);
      if (!saveCmds.HasValue) return;

      var cmdFile = "";
      bool open = false;

      if (saveCmds.Value)
      {
        cmdFile = ed.GetFilename("Command file to write", false);
        open = false;
      }
      else
      {
        var openCmds = ed.GetYesOrNo("Read commands from file?", false);
        if (!openCmds.HasValue) return;

        if (openCmds.Value)
        {
          cmdFile = ed.GetFilename("Command file to read", true);
          open = true;
        }
      }

      try
      {
        var gameStream =
          new FileStream(gameFile, FileMode.Open, FileAccess.Read);

        var io = new AutoCADIONodeMap(ed, showScore.Value, cmdFile);
        var zm = new ZMachine(gameStream, io);
        if (!String.IsNullOrWhiteSpace(cmdFile))
        {
          if (open)
            zm.ReadingCommandsFromFile = true;
          else
            zm.WritingCommandsToFile = true;
        }
        zm.PredictableRandom = false;
        zm.Run();
      }
      catch (System.Exception ex)
      {
        ed.WriteMessage("{0} ({1})", ex.Message, ex.GetType().Name);
      }
    }

    [CommandMethod("ZMAP")]
    public void ZMap()
    {
      var asm = Assembly.GetExecutingAssembly();
      var loc = Path.GetDirectoryName(asm.Location) + "\\index.html";

      _isops =
        ShowPalette(
          _isops,
          new Guid("FFE8CBF9-752A-461C-8A71-55AEF29F3CA6"),
          "ZMAP",
          "Zork Map",
          new System.Uri(loc)
        );
    }

    [CommandMethod("ZMOUT")]
    public void ZMapOut()
    {
      var graph = acjsInvokeAsync("getgraph", "{}");
    }

    [JavaScriptCallback("CreateGraph")]
    public string CreateGraph(string jsonArgs)
    {
      // The radius for our node circles and the height of our text

      const double rad = 20, txtHeight = 10;

      var doc = Application.DocumentManager.MdiActiveDocument;
      if (doc == null) return "";
      var db = doc.Database;
      var ed = doc.Editor;

      JArray jedges = null, jnodes = null;

      using (var sr = new StringReader(jsonArgs))
      {
        var jr = new JsonTextReader(sr);
        var js = new JsonSerializer();
        var jo = js.Deserialize(jr) as JObject;
        if (jo != null)
        {
          foreach (var jp in jo.Properties())
          {
            if (jp.Name == "edges")
            {
              jedges = jp.Value as JArray;
            }
            else if (jp.Name == "nodes")
            {
              jnodes = jp.Value as JArray;
            }
          }
        }
      }

      var nodes = new Dictionary<String, Node>();
      var edges = new Dictionary<String, Edge>();

      GetNodesAndEdges(jnodes, jedges, nodes, edges);

      if (nodes.Count > 0)
      {
        using (var dlock = doc.LockDocument())
        {
          using (var tr = doc.TransactionManager.StartTransaction())
          {
            var btr =
              (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForWrite
              );

            // Rather than add the entities to the database directly,
            // gather them in a collection as we'll check their extents
            // and transform them when we add them

            var ents = new List<Entity>();
 
            // Create our nodes

            foreach (var kvnode in nodes)
            {
              var node = kvnode.Value;

              // A node is a circle with some text

              ents.Add(new Circle(node.Position, Vector3d.ZAxis, rad));
              ents.Add(CreateText(node.Position, txtHeight, node.Name));
            }

            // Create our edges

            foreach (var kvedge in edges)
            {
              var edge = kvedge.Value;

              // Edges that go both ways will be arcs

              bool createArc = edges.ContainsKey(ReverseEdge(kvedge.Key));

              // An edge is a curve with an arrowhead and some text

              var c = CreateEdge(rad, edge, createArc);
              ents.Add(c);
              ents.Add(CreateArrowhead(txtHeight, c));
              ents.Add(CreateText(c.MidPoint(), txtHeight, edge.Name));
            }

            // Collect the extents of our entities

            var ext = new Extents3d();            
            foreach (var ent in ents)
            {
              if (ent.Bounds.HasValue)
                ext.AddExtents(ent.Bounds.Value);
            }
            
            // We'll displace them to start at the origin (as they're
            // created in negative space - as we negate the Y value -
            // we can assume the MaxPoint is going to be the origin)

            var m = Matrix3d.Displacement(-ext.MinPoint.GetAsVector());
            foreach (var ent in ents)
            {
              ent.TransformBy(m);
              btr.AppendEntity(ent);
              tr.AddNewlyCreatedDBObject(ent, true);
            }

            // Commit the transaction before we finish

            tr.Commit();
          }

          // Zoom to the extents of our map

          doc.SendStringToExecute("_.ZOOM _E ", false, false, false);
        }
      }

      return "";
    }

    // Extract node and edge objects from our JSON data

    private void GetNodesAndEdges(
      JArray jnodes, JArray jedges,
      Dictionary<String, Node> nodes, Dictionary<String, Edge> edges
    )
    {
      foreach (JObject jnode in jnodes)
      {
        var d = jnode["data"];
        var p = jnode["pos"];
        var node = new Node();
        node.Name = d["name"].ToString();
        node.Position =
          new Point3d(
            Double.Parse(p["x"].ToString()),
            -Double.Parse(p["y"].ToString()),
            0
          );
        nodes.Add(d["id"].ToString(), node);
      }

      foreach (JObject jedge in jedges)
      {
        var edge = new Edge();
        edge.Name = jedge["name"].ToString();
        var src = jedge["source"].ToString();
        var trg = jedge["target"].ToString();
        edge.Start = nodes[src].Position;
        edge.End = nodes[trg].Position;
        edges.Add(jedge["id"].ToString(), edge);
      }
    }
    
    // Geometry creation helper functions

    private static DBText CreateText(Point3d pos, double height, string txt)
    {
      var t = new DBText();
      t.Normal = Vector3d.ZAxis;
      t.Position = pos;
      t.Justify = AttachmentPoint.MiddleCenter;
      t.AlignmentPoint = pos;
      t.TextString = txt;
      t.Height = height;
      return t;
    }

    private static Curve CreateEdge(double rad, Edge edge, bool createArc)
    {
      Curve c = null;

      if (createArc)
      {
        // Calculate a mid-point for our arc

        var vec = edge.End - edge.Start;
        var len = vec.Length;
        var vmid = edge.Start + (vec * 0.5);
        vec = vec.RotateBy(Math.PI * 0.5, Vector3d.ZAxis);
        vec = -0.1 * vec;
        var mid = vmid + vec;

        // Create the initial arc between the center points of the
        // two nodes, as well as circles around the nodes

        var ca = new CircularArc3d(edge.Start, mid, edge.End);
        var c1 = new CircularArc3d(edge.Start, Vector3d.ZAxis, rad);
        var c2 = new CircularArc3d(edge.End, Vector3d.ZAxis, rad);

        // Intersect the arc with the two circles

        var pts1 = ca.IntersectWith(c1);
        var pts2 = ca.IntersectWith(c2);

        // Adjust the start and end of the arc, effectively trimming
        // it to the circles

        var newStart = edge.Start;
        var newEnd = edge.End;
        if (pts1 != null && pts1.Length > 0)
          newStart = pts1[0];
        if (pts2 != null && pts2.Length > 0)
          newEnd = pts2[0];

        // Create our new, trimmed arc, and the database version of it

        var ca2 = new CircularArc3d(newStart, mid, newEnd);
        c = Arc.CreateFromGeCurve(ca2);
      }
      else
      {
        // Create the line - adjusted to go from the node circles)
        // and add it to the database

        var vec = edge.End - edge.Start;
        var unit = vec / vec.Length;

        c = new Line(edge.Start + unit * rad, edge.End - unit * rad);
      }

      return c;
    }

    private static Entity CreateArrowhead(double arrowSize, Curve c)
    {
      // Create the arrowhead

      var s =
        new Solid(
          new Point3d(-0.25 * arrowSize, -arrowSize, 0),
          Point3d.Origin,
          new Point3d(0.25 * arrowSize, -arrowSize, 0)
        );

      // Tweak the rotation of the arrowhead depending on the arrival
      // angle of the edge

      var vec2 = c.GetFirstDerivative(c.EndParam);
      var rot = (new Vector3d(0, 1, 0)).GetAngleTo(vec2);
      if (vec2.X > 0) // If the arrival is from the left, flip it
        rot = -rot;
      var m =
        Matrix3d.Displacement(c.EndPoint - Point3d.Origin).
          PostMultiplyBy(
            Matrix3d.Rotation(
              rot,
              Vector3d.ZAxis,
              Point3d.Origin
            )
          );
      s.TransformBy(m);
      return s;
    }

    // Edge ids are "src-trg": to find the reverse direction we want "trg-src"

    private string ReverseEdge(string id)
    {
      const string sep = "-";
      return
        id.Contains(sep) ?
          id.Substring(id.IndexOf(sep) + 1) + sep +
          id.Substring(0, id.IndexOf(sep)) : "";
    }
    
    // Helper function to show a palette

    private static PaletteSet ShowPalette(
      PaletteSet ps, Guid guid, string cmd, string title, Uri uri,
      bool reload = false
    )
    {
      // If the reload flag is true we'll force an unload/reload
      // (this isn't strictly needed - given our refresh function -
      // but I've left it in for possible future use)

      if (reload && ps != null)
      {
        // Close the palette and make sure we process windows
        // messages, otherwise sizing is a problem

        ps.Close();
        System.Windows.Forms.Application.DoEvents();
        ps.Dispose();
        ps = null;
      }

      if (ps == null)
      {
        ps = new PaletteSet(cmd, guid);
      }
      else
      {
        if (ps.Visible)
          return ps;
      }

      if (ps.Count != 0)
        ps.Remove(0);

      ps.Add(title, uri);
      ps.Visible = true;

      return ps;
    }
  }
}
