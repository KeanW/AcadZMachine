using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ZLR.VM;

namespace AcadZMachine
{
  class AutoCADIONodeMap : IZMachineIO
  {
    [DllImport(
      "AcJsCoreStub.crx", CharSet = CharSet.Auto,
      CallingConvention = CallingConvention.Cdecl,
      EntryPoint = "acjsInvokeAsync")]
    extern static private int acjsInvokeAsync(
      string name, string jsonArgs
    );

    private string _suppliedCommandFile;
    private Editor _ed;
    private short _curWin = 0;
    private bool _showProgress;
    private List<string> _non0 = new List<string>();

    private Point2d _position = Point2d.Origin;
    private string _lastMove = "", _lastLoc = "";
    private ushort _curLoc = 0;

    const int locIdx = 1;
    const int scoIdx = 52;
    const int movIdx = 119;

    public AutoCADIONodeMap(
      Editor ed, bool showProgress = false, string commandFile = null
    )
    {
      _suppliedCommandFile = commandFile;
      _ed = ed;
      _showProgress = showProgress;
    }

    public void SetLocation(ushort s)
    {
      _curLoc = s;
    }

    public string ReadLine(
      string initial, int time, TimedInputCallback callback,
      byte[] terminatingKeys, out byte terminator
    )
    {
      if (_non0.Count == 199)
      {
        _lastLoc = _non0[locIdx];

        var sb = new StringBuilder();
        sb.AppendFormat(
          "{{\"id\":\"{0}\",\"name\":\"{1}\",\"dir\":\"{2}\"}}",
          _curLoc, _lastLoc, _lastMove.ToUpper()
        );
        acjsInvokeAsync("setloc", sb.ToString());
      }

      terminator = 13;
      var prompt =
        _non0.Count == 199 && _showProgress ?
          String.Format(
            "\n<Location: {0}, Score {1}, Moves {2}> ",
            _non0[locIdx],
            _non0[scoIdx],
            _non0[movIdx]
          ) : "";
      var pso = new PromptStringOptions(prompt);
      pso.AllowSpaces = true;
      var pr = _ed.GetString(pso);

      if (pr.Status == PromptStatus.OK && pr.StringResult == "-")
      {
        pr = _ed.GetString(pso);
      }
      _lastMove = pr.Status == PromptStatus.OK ? pr.StringResult : "";
      _non0.Clear();

      return _lastMove;
    }

    public short ReadKey(
      int time, TimedInputCallback callback, CharTranslator translator
    )
    {
      /*
      short ch;
      do
      {
        ConsoleKeyInfo info = Console.ReadKey();
        ch = translator(info.KeyChar);
      } while (ch == 0);
      */
      _ed.WriteMessage("\nReadKey called.\n");
      return 0;
    }

    public void PutCommand(string command)
    {
      // nada
    }

    public void PutChar(char ch)
    {
      PutString(ch.ToString());
    }

    public void PutString(string str)
    {
      if (_curWin == 0)
        //_ed.WriteMessage("{0}[{1}]", str, _curWin);
        _ed.WriteMessage(str);
      else
        _non0.Add(str);
    }

    public void PutTextRectangle(string[] lines)
    {
      foreach (string str in lines)
        PutString(str);
    }

    public bool Buffering
    {
      get { return false; }
      set { /* nada */ }
    }

    public bool Transcripting
    {
      get { return false; }
      set { /* nada */ }
    }

    public void PutTranscriptChar(char ch)
    {
      // not implemented
    }

    public void PutTranscriptString(string str)
    {
      // not implemented
    }

    public System.IO.Stream OpenSaveFile(int size)
    {
      // not implemented
      return null;
    }

    public System.IO.Stream OpenRestoreFile()
    {
      // not implemented
      return null;
    }

    public System.IO.Stream OpenAuxiliaryFile(
      string name, int size, bool writing
    )
    {
      // not implemented
      return null;
    }

    public System.IO.Stream OpenCommandFile(bool writing)
    {
      string filename;
      if (_suppliedCommandFile != null)
      {
        filename = _suppliedCommandFile;
        _suppliedCommandFile = null;
      }
      else
      {
        do
        {
          var prompt =
            String.Format(
              "Enter the name of a command file to {0}",
              writing ? "record" : "play back"
            );

          filename = _ed.GetFilename(prompt, !writing);
          if (String.IsNullOrWhiteSpace(filename))
            return null;

          if (writing)
          {
            // If the file exists, prompt to overwrite it

            if (File.Exists(filename))
            {
              var prompt2 =
                String.Format("\"{0}\" exists. Are you sure", filename);

              var overWrite = _ed.GetYesOrNo(prompt2, false);
              if (overWrite.HasValue && overWrite.Value)
                break;
            }
            else
              break;
          }
          else
          {
            // The file must already exist

            if (File.Exists(filename))
              break;
          }
        }
        while (true);
      }

      return new FileStream(filename,
        writing ? FileMode.Create : FileMode.Open,
        writing ? FileAccess.Write : FileAccess.Read
      );
    }

    public void SetTextStyle(TextStyle style)
    {
      // nada
    }

    public void SplitWindow(short lines)
    {
      // nada
    }

    public void SelectWindow(short num)
    {
      _curWin = num;
    }

    public void EraseWindow(short num)
    {
      // nada
    }

    public void EraseLine()
    {
      // nada
    }

    public void MoveCursor(short x, short y)
    {
      // nada
    }

    public void GetCursorPos(out short x, out short y)
    {
      x = 1;
      y = 1;
    }

    public void SetColors(short fg, short bg)
    {
      // nada
    }

    public short SetFont(short num)
    {
      return 0;
    }

    public void PlaySoundSample(
      ushort number, SoundAction action, byte volume,
      byte repeats, SoundFinishedCallback callback
    )
    {
      // nada
    }

    public void PlayBeep(bool highPitch)
    {
      // nada
    }

    public bool ForceFixedPitch
    {
      get { return false; }
      set { /* nada */ }
    }

    public bool BoldAvailable
    {
      get { return false; }
    }

    public bool ItalicAvailable
    {
      get { return false; }
    }

    public bool FixedPitchAvailable
    {
      get { return false; }
    }

    public bool VariablePitchAvailable
    {
      get { return false; }
    }

    public bool ScrollFromBottom
    {
      get { return false; }
      set { /* nada */ }
    }

    public bool GraphicsFontAvailable
    {
      get { return false; }
    }

    public bool TimedInputAvailable
    {
      get { return false; }
    }

    public bool SoundSamplesAvailable
    {
      get { return false; }
    }

    public byte WidthChars
    {
      get { return 80; }
    }

    public short WidthUnits
    {
      get { return 80; }
    }

    public byte HeightChars
    {
      get { return 25; }
    }

    public short HeightUnits
    {
      get { return 25; }
    }

    public byte FontHeight
    {
      get { return 1; }
    }

    public byte FontWidth
    {
      get { return 1; }
    }

    public event EventHandler SizeChanged
    {
      add { /* nada */ }
      remove { /* nada */ }
    }

    public bool ColorsAvailable
    {
      get { return false; }
    }

    public byte DefaultForeground
    {
      get { return 9; }
    }

    public byte DefaultBackground
    {
      get { return 2; }
    }

    public UnicodeCaps CheckUnicode(char ch)
    {
      return UnicodeCaps.CanInput | UnicodeCaps.CanPrint;
    }

    public bool DrawCustomStatusLine(
      string location, short hoursOrScore, short minsOrTurns, bool useTime
    )
    {
      return false;
    }
  }
}
