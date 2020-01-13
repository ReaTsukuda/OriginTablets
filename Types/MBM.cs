using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OriginTablets.Support;
using System.Text.RegularExpressions;

namespace OriginTablets.Types
{
  public class MBM : List<string>
  {
    /// <summary>
    /// Whether or not null entries cause the entry index to increment.
    /// EO3 to EO2U do not do this, EO5 and EON do.
    /// </summary>
    private bool NullEntriesWriteIndex = false;

    /// <summary>
    /// Documented control codes and their variables. The key is the control code.
    /// If a control code has no variables, then its value is 0. If a control code
    /// has a variable, than its value is the amount of shorts that comprise that variable.
    /// </summary>
    private Dictionary<int, int> RecognizedControlCodes = new Dictionary<int, int>()
    {
      // EO3: linebreak.
      { 0x8001, 0 },
      // EO3: new page.
      { 0x8002, 0 },
      // EO3: text color. Variable is the text color ID.
      { 0x8004, 1 },
      // EO3: guild name.
      { 0x8040, 0 },
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
    /// This empty constructor is used for serialization.
    /// </summary>
    public MBM()
    {

    }

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
          int entryIndex = reader.ReadInt32();
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
                int compositeControlCode = (upperControlCode << 8) + lowerControlCode;
                buffer.Append(string.Format("[{0:X2} {1:X2}]", upperControlCode, lowerControlCode));
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
            // If a null entry has a non-zero indiex, then we're dealing with an EO5/EON MBM.
            if (entryIndex != 0)
            {
              NullEntriesWriteIndex = true;
            }
          }
        }
      }
    }

    /// <summary>
    /// Save the MBM to a file.
    /// </summary>
    /// <param name="location">Where to save the MBM to.</param>
    public void WriteToFile(string location)
    {
      var encodedStrings = new List<byte[]>();
      foreach (string entry in this)
      {
        if (entry != null)
        {
          encodedStrings.Add(GetEncodedString(entry));
        }
        else
        {
          encodedStrings.Add(new byte[0]);
        }
      }
      using (var output = new BinaryWriter(new FileStream(location, FileMode.Create), Encoding.GetEncoding("shift_jis")))
      {
        // Header writing.
        output.Write(0x0);
        output.Write((byte)0x4D);
        output.Write((byte)0x53);
        output.Write((byte)0x47);
        output.Write((byte)0x32); // MSG2 magic number.
        output.Write(0x00000100);
        output.Write(0x0); // It's probably safe to write a file size of 0. Probably.
        output.Write(Count); // Number of entries.
        output.Write(0x00000020);
        output.Write(0x0);
        output.Write(0x0);
        // How many non-null entries we've written.
        // This isn't strictly necessary for EON, but I don't see how it would hurt.
        int internalIndex = 0;
        // Our current theoretical position in the string section.
        // The initial position can be calculated as the header length plus
        // the number of entries multiplied by 0x10. Each entry is 0x10 bytes.
        int stringPosition = 0x20 + (this.Count() * 0x10);
        // Entry section writing.
        // We do a for loop instead of a foreach to make sure we catch null entries.
        foreach (byte[] entry in encodedStrings)
        {
          // Write the individual components of an entry table entry.
          if (NullEntriesWriteIndex == true
            || (NullEntriesWriteIndex == false && entry.Length > 0))
          {
            output.Write(internalIndex);
          }
          else
          {
            output.Write(0x0);
          }
          // If the entry is null, write 0 for both length and position.
          if (entry.Length > 0) { output.Write(entry.Length); }
          else { output.Write(0x0); }
          if (entry.Length > 0) { output.Write(stringPosition); }
          else { output.Write(0x0); }
          output.Write(0x0);
          // After we've written a string position, we need to update the current position
          // for the next string.
          stringPosition += entry.Length;
          // Throwing a null object to ConvertControlCodeToBytes will cause an exception.
          // So, don't do that.
          internalIndex += 1;
        }
        // String writing.
        foreach (byte[] entry in encodedStrings)
        {
          if (entry != null)
          {
            output.Write(entry);
          }
        }
      }
    }

    /// <summary>
    /// Takes a string with human-readable representations of control codes, and
    /// converts the instances of control codes back to control code bytes. Also
    /// converts strings back into Shift-JIS bytes.
    /// </summary>
    /// <param name="input">The string to be converted.</param>
    private byte[] GetEncodedString(string input)
    {
      // Find each unique control code in the string. For now, it should be
      // safe to assume that only control codes will use [] characters.
      var controlCodes = Regex.Matches(input, "\\[.*?\\]")
        .Cast<Match>()
        .Select(match => match.Value)
        .Distinct();
      // We detect and remove control codes here, since otherwise parsing them and
      // encoding them is a complete nightmare. Every instance of a control code is
      // logged, and then substituted for 0xFFFF, to be replaced once we've encoded
      // the string into Shift-JIS.
      var positionsAndControlCodes = new SortedDictionary<int, string>();
      foreach (string controlCode in controlCodes)
      {
        // Log where each instance of a control code was.
        var indices = Regex.Matches(input, controlCode.Replace("[", "\\[").Replace("]", "\\]"))
          .Cast<Match>()
          .Select(match => match.Index);
        // Prepare a buffer for output.
        var buffer = new StringBuilder(input);
        foreach (int index in indices)
        {
          positionsAndControlCodes.Add((index * 2), controlCode);
          // Substitute the control code strings for x instances of 0x0, where
          // x is the length of the replaced string.
          for (int controlCodeIndex = 0; controlCodeIndex < controlCode.Length; controlCodeIndex += 1)
          {
            buffer[index + controlCodeIndex] = '-';
          }
        }
        input = buffer.ToString();
      }
      // Substitute halfwidth characters with fullwidth characters, to prepare for encoding.
      var fullwidthBuffer = new StringBuilder(input);
      for (int characterIndex = 0; characterIndex < input.Length; characterIndex += 1)
      {
        char character = input[characterIndex];
        if (SJISTables.ToFullwidth.ContainsKey(character))
        {
          fullwidthBuffer[characterIndex] = SJISTables.ToFullwidth[character];
        }
      }
      input = fullwidthBuffer.ToString();
      var encodedString = Encoding.GetEncoding("shift_jis").GetBytes(input).ToList();
      // Substitute all those null characters we injected with the control code bytes.
      foreach (KeyValuePair<int, string> controlCode in positionsAndControlCodes)
      {
        // Replace the first two placeholder null bytes with the control code.
        string[] splitControlCode = controlCode.Value.Replace("[", "").Replace("]", "").Split(' ');
        byte upperControlCode = byte.Parse(splitControlCode[0], System.Globalization.NumberStyles.HexNumber);
        byte lowerControlCode = byte.Parse(splitControlCode[1], System.Globalization.NumberStyles.HexNumber);
        encodedString[controlCode.Key] = upperControlCode;
        encodedString[controlCode.Key + 1] = lowerControlCode;
      }
      // Now that we've put all the control code bytes back in, we need to remove the
      // placeholder null bytes.
      for (int processedControlCodes = 0; processedControlCodes < positionsAndControlCodes.Count(); processedControlCodes += 1)
      {
        // We know where to remove stuff from by deducting 12 bytes from the index
        // for each removal we've already performed. Every placeholder will always be 14 bytes.
        var removalStartIndex = positionsAndControlCodes.ElementAt(processedControlCodes).Key
          - (processedControlCodes * 12) + 2;
        encodedString.RemoveRange(removalStartIndex, 12);
      }
      // Add the 0xFFFF terminator.
      encodedString.Add(0xFF);
      encodedString.Add(0xFF);
      return encodedString.ToArray();
    }
  }
}
