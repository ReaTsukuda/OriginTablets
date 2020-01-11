using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OriginTablets.Support;

namespace OriginTablets.Types
{
  public class MBM : List<string>
  {
      /// <summary>
      /// Documented control codes and their variables. The key is the control code.
      /// If a control code has no variables, then its value is 0. If a control code
      /// has a variable, than its value is the amount of shorts that comprise that variable.
      /// </summary>
      private Dictionary<int, int> RecognizedControlCodes = new Dictionary<int, int>()
      {
        // EO2U and on: set NPC telop to the upcoming string, until [00 00] is hit.
        { 0xF812, 0 },
        // EOU to EO2U: play voice clip. First short is the voice group ID, second short is the clip ID.
        { 0xF813, 4 },
        // EO5 to EON: play voice clip. Variable is a null-terminated ASCII string pointing to the clip.
        // This requires special handling.
        { 0xF81B, 0 },
        // EO4 and on: retrieve skill subheader value. Value is the subheader ID.
        { 0xF85A, 1 },
        // EO2U and on: set recognized NPC telop. Value is the NPC telop ID.
        { 0xF8F9, 1 }
    };

    /// <summary>
    /// For loading and modifying EO text archives (MBMs).
    /// </summary>
    /// <param name="location">The location of the MBM file to load.</param>
    public MBM(string location)
    {
      using (var reader = new BinaryReader(new FileStream(location, FileMode.Open), Encoding.GetEncoding("shift_jis")))
      {
        reader.ReadInt32(); // Always 0x00000000.
        reader.ReadInt32(); // MSG2 magic number.
        reader.ReadInt32(); // Unknown; always 0x00010000.
        reader.ReadInt32(); // File size, excluding null entries.
        // This is supposed to be the number of entries, but it cannot be relied on for most EO games.
        reader.ReadUInt32();
        uint entryTablePointer = reader.ReadUInt32();
        reader.ReadInt32(); // Unused.
        reader.ReadInt32(); // Unused.
        reader.BaseStream.Seek(entryTablePointer, SeekOrigin.Begin);
        // Since we can't rely on the number of entries, the way we check for end of the entry table
        // is a bit convoluted. We have to parse entries until we find the first non-null one,
        // and then mark its location as the end of the entry table. Until we find that non-null
        // entry, we have to set the end of the entry table to a very high value to keep the loop
        // from breaking.
        long entryTableEndAddress = long.MaxValue;
        bool parsedNonNullEntry = false;
        var buffer = new StringBuilder();
        while (reader.BaseStream.Position < entryTableEndAddress)
        {
          reader.ReadInt32(); // Entry index. We can ignore this.
          uint entryLength = reader.ReadUInt32();
          uint stringPointer = reader.ReadUInt32();
          reader.ReadInt32(); // Always 0x00000000.
          // If this entry is not null, and we have not parsed a non-null entry yet,
          // mark that we have, and set the entry table end address accordingly.
          if (entryLength > 0 && stringPointer > 0 && parsedNonNullEntry == false)
          {
            parsedNonNullEntry = true;
            entryTableEndAddress = stringPointer;
          }
          // If this entry is not null, add its string.
          if (entryLength > 0 && stringPointer > 0)
          {
            long storedPosition = reader.BaseStream.Position;
            reader.BaseStream.Seek(stringPointer, SeekOrigin.Begin);
            // String construction can be halted once the next character is 0xFF.
            // Since PeekChar() returns an actual character, we have to do this an ugly way.
            while (true)
            {
              // To get the upcoming prefix byte, we have to read one ahead, and then reset our position.
              // It's ugly, I know.
              byte prefixByte = reader.ReadByte();
              reader.BaseStream.Seek(-1, SeekOrigin.Current);
              // If the prefix byte is 0xFF, end the current loop.
              if (prefixByte == 0xFF)
              {
                break;
              }
              // If the prefix byte is less than 0x81, between 0xF0 and 0xF9, or between 0xA0 and 0xE0,
              // then we've hit a control code, and need to prettify it.
              else if (prefixByte < 0x81 
                || (prefixByte >= 0xA0 && prefixByte <= 0xE0)
                || (prefixByte >= 0xF0 && prefixByte <= 0xF9))
              {
                byte upperControlCode = reader.ReadByte();
                byte lowerControlCode = reader.ReadByte();
                buffer.Append(string.Format("[{0:X2} {1:X2}]", upperControlCode, lowerControlCode));
                int compositeControlCode = (upperControlCode << 8) + lowerControlCode;
                // We need a special override for EO5 and EON's voice clip control code, since that
                // precedes a null-terminated ASCII string giving the location of the voice clip.
                if (compositeControlCode == 0xF81B)
                {
                  var voiceClipLocationBuffer = new StringBuilder();
                  // Again, we have to handle this in a kind of ugly way due to
                  // the reader having SJIS encoding set.
                  while (true)
                  {
                    char currentCharacter = (char)reader.ReadByte();
                    if (currentCharacter != 0x0)
                    {
                      voiceClipLocationBuffer.Append(currentCharacter);
                    }
                    // Once we hit a null terminator, break the loop.
                    else
                    {
                      // The null terminator is technically two null bytes.
                      // I wish I knew why. In any case, we have to eat the second one.
                      reader.ReadByte();
                      // This is to make the voice clip location blend in less with dialogue.
                      voiceClipLocationBuffer.Append("[00 00]");
                      break;
                    }
                  }
                  buffer.Append(voiceClipLocationBuffer.ToString());
                }
                // If the control code is in the list of recognized control codes, then grab its
                // variables, if necessary.
                // TODO: Figure out a way to better format variables for, say, EOU/EO2U voice clip calls.
                else if (RecognizedControlCodes.ContainsKey(compositeControlCode))
                {
                  for (int variable = 0; variable < RecognizedControlCodes[compositeControlCode]; variable += 1)
                  {
                    byte upperValue = reader.ReadByte();
                    byte lowerValue = reader.ReadByte();
                    buffer.Append(string.Format("[{0:X2} {1:X2}]", upperValue, lowerValue));
                  }
                }
              }
              // Otherwise, it's a normal character, and we should add it normally.
              else if (prefixByte != 0xFF)
              {
                char nextChar = reader.ReadChar();
                if (SJISTables.FromFullwidth.ContainsKey(nextChar))
                {
                  buffer.Append(SJISTables.FromFullwidth[nextChar]);
                }
                else { buffer.Append(nextChar); }
              }
            }
            // Once we're done constructing the string, log it, clear the buffer,
            // and put us back in the entry table.
            Add(buffer.ToString());
            buffer.Clear();
            reader.BaseStream.Seek(storedPosition, SeekOrigin.Begin);
          }
          // If the entry is null, add a null.
          else
          {
            Add(null);
          }
        }
      }
    }
  }
}
