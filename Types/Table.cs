using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OriginTablets.Support;

namespace OriginTablets.Types
{
  /// <summary>
  /// For loading and modifying text tables, such as the skill name tables.
  /// The file extension for these, as far as I'm aware, is always tbl.
  /// </summary>
  public class Table : List<string>
  {
    /// <summary>
    /// Constructor for loading Table objects from tbl files.
    /// </summary>
    /// <param name="location">The path to the requested tbl file.</param>
    /// <param name="longPointers">Whether or not the tbl file uses 4-byte pointers instead of 2-byte pointers.</param>
    public Table(string location, bool longPointers)
    {
      using (var reader = new BinaryReader(new FileStream(location, FileMode.Open), Encoding.GetEncoding("shift_jis")))
      {
        var numberOfEntries = reader.ReadUInt16();
        // We skip parsing the pointers since we can just read entries in sequence.
        // Since all pointers have a consistent length, the amount of bytes that we need to skip
        // can be found through calculating the pointer length multiplied by the number of entries.
        int pointerLength = longPointers == true ? 4 : 2;
        reader.BaseStream.Seek(numberOfEntries * pointerLength, SeekOrigin.Current);
        // This is where we construct strings from the binary data.
        var buffer = new StringBuilder();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          // Add to the current string if the next byte isn't a null terminator.
          if (reader.PeekChar() != 0x0)
          {
            var nextChar = reader.ReadChar();
            // Add the Unicode equivalent of the next character, if we have one.
            if (SJISTables.FromFullwidth.ContainsKey(nextChar)) { buffer.Append(SJISTables.FromFullwidth[nextChar]); }
            // Otherwise, just add the character.
            else { buffer.Append(nextChar); }
          }
          // Once we hit a null terminator, we add whatever's in the buffer to the list,
          // clear out the buffer, and advance past the terminator.
          else
          {
            Add(buffer.ToString());
            buffer.Clear();
            reader.ReadByte();
          }
        }
      }
    }
  }
}
