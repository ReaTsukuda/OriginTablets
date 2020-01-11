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
        uint numberOfEntries = 0;
        if (longPointers == true) { numberOfEntries = reader.ReadUInt32(); }
        else { numberOfEntries = reader.ReadUInt16(); }
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

    /// <summary>
    /// Writes the table to a .tbl file.
    /// </summary>
    /// <param name="location">Where to write the .tbl file to.</param>
    public void WriteToFile(string location, bool longPointers)
    {
      using (var output = new BinaryWriter(
        new FileStream(location, FileMode.Create), Encoding.GetEncoding("shift_jis")))
      {
        if (longPointers == true) { output.Write(Count); }
        else { output.Write((ushort)Count); }
        // Write pointers to each text entry. Each pointer's location is equal to the byte length
        // of the previous entry, plus 1 for the null terminator. An entry's byte length is equal to
        // the number of characters in it multiplied by 2, plus the previous entry's pointer. 
        // Pointers are unsigned shorts if longPointers is false, and unsigned ints otherwise. 
        // A pointer is not written for the first entry, since the games assume its address is 0x0.
        int lastOffset = 0;
        for (int index = 1; index < Count; index += 1)
        {
          int result = 0;
          // The first entry can just write its own length and store it.
          // Beyond that, each entry needs to add the previous entry's offset to its own basic length.
          if (longPointers == true)
          {
            result = (this[index - 1].Length * 4) + 1 + lastOffset;
            output.Write(result);
          }
          else
          {
            result = (this[index - 1].Length * 2) + 1 + lastOffset;
            output.Write((ushort)result);
          }
          lastOffset = result;
        }
        // A final pointer needs to be written to, pointing to the end of file.
        // This is equal to the last offset plus the length of the last string.
        if (longPointers == true) { output.Write((this.Last().Length * 4) + 1 + lastOffset); }
        else { output.Write((ushort)((this.Last().Length * 2) + 1 + lastOffset)); }
        // Write the bytes for each string. Once they've been fully written, write a null terminator.
        foreach (string entry in this)
        {
          foreach (char character in entry)
          {
            char fullwidthCharacter = character;
            if (SJISTables.ToFullwidth.ContainsKey(fullwidthCharacter))
            {
              fullwidthCharacter = SJISTables.ToFullwidth[fullwidthCharacter];
            }
            output.Write(fullwidthCharacter);
          }
          output.Write((byte)0x0);
        }
      }
    }
  }
}
