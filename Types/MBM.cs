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
    /// Strings for making documented control codes more recognizable. You can view comments
    /// on control codes at RecognizableControlCodes.
    /// </summary>
    private Dictionary<int, string> ControlCodeStrings = new Dictionary<int, string>()
    {
      { 0x8001, "EO3Linebreak" },
      { 0x8002, "EO3NewPage" },
      { 0x8004, "EO3TextColor" },
      { 0x8040, "EO3GuildName" },
      { 0xF812, "CustomTelop" },
      { 0xF813, "OldVoice" },
      { 0xF81B, "Voice" },
      { 0xF85A, "SubheaderValue" },
      { 0xF8F9, "Telop" }
    };

    /// <summary>
    /// Effectively the inverse of ControlCodeStrings: this is for turning human-readable
    /// representations of control codes back into control codes.
    /// </summary>
    private Dictionary<string, byte[]> StringControlCodes = new Dictionary<string, byte[]>()
    {
      { "[EO3Linebreak]", new byte[] { 0x80, 0x01 } },
      { "[EO3NewPage]", new byte[] { 0x80, 0x02 } },
      { "[EO3TextColor]", new byte[] { 0x80, 0x04 } },
      { "[EO3GuildName]", new byte[] { 0x80, 0x40 } },
      { "[CustomTelop]", new byte[] { 0xF8, 0x12 } },
      { "[OldVoice]", new byte[] { 0xF8, 0x13 } },
      { "[Voice]", new byte[] { 0xF8, 0x1B } },
      { "[SubheaderValue]", new byte[] { 0xF8, 0x5A } },
      { "[Telop]", new byte[] { 0xF8, 0xF9 } }
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
                int compositeControlCode = (upperControlCode << 8) + lowerControlCode;
                if (ControlCodeStrings.ContainsKey(compositeControlCode))
                {
                  buffer.Append(string.Format("[{0}]", ControlCodeStrings[compositeControlCode]));
                }
                else
                {
                  buffer.Append(string.Format("[{0:X2} {1:X2}]", upperControlCode, lowerControlCode));
                }
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

    /// <summary>
    /// Save the MBM to a file.
    /// </summary>
    /// <param name="location">Where to save the MBM to.</param>
    public void WriteToFile(string location)
    {
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
        // Entry section writing.
        // We do a for loop instead of a foreach to make sure we catch null entries.
        foreach (string entry in this)
        {
          // Throwing a null object to ConvertControlCodeToBytes will cause an exception.
          // So, don't do that.
          if (entry != null)
          {
            var result = Encoding.Default.GetBytes(ConvertControlCodesToBytes(entry));
            output.Write(result);
          }
        }
      }
    }

    /// <summary>
    /// Takes a string with human-readable representations of control codes, and
    /// converts the instances of control codes back to control code bytes. Also
    /// converts strings back into Shift-JIS.
    /// </summary>
    /// <param name="input">The string to be converted.</param>
    private string ConvertControlCodesToBytes(string input)
    {
      // Gets every unique control code used in the string.
      var controlCodes = Regex.Matches(input, "\\[.*?\\]")
        .Cast<Match>()
        .Select(match => match.Value)
        .Distinct();
      // Replaces all instances of each unique control code with the appropriate bytes.
      foreach (string controlCodeString in controlCodes)
      {
        // If the control code follows the numerical pattern, then parse the numbers.
        if (Regex.IsMatch(controlCodeString, "\\[[0-9A-F][0-9A-F] [0-9A-F][0-9A-F]\\]"))
        {
          // Get rid of the brackets in the string so we can parse it more easily.
          string temporaryControlCodeString = controlCodeString.Replace("[", "").Replace("]", "");
          // All of this weird value coercion stuff is all because C# doesn't easily let you
          // insert raw bytes into strings. Ugh.
          string upperControlCodeString = temporaryControlCodeString.Split(' ')[0];
          char upperControlCode = (char)Convert.ToByte(upperControlCodeString, 16);
          string lowerControlCodeString = temporaryControlCodeString.Split(' ')[1];
          char lowerControlCode = (char)Convert.ToByte(lowerControlCodeString, 16);
          string tokenToReplaceWith = string.Format("{0}{1}", upperControlCode, lowerControlCode);
          input = input.Replace(controlCodeString, tokenToReplaceWith);
        }
        // Otherwise, replace it with the pattern associated with the known string.
        else
        {
          input = input.Replace(controlCodeString,
            Encoding.GetEncoding("shift_jis").GetString(StringControlCodes[controlCodeString]));
        }
      }
      // We need a temporary buffer for putting SJIS characters back in,
      // since modifying input mid-loop would cause an exception.
      string temporaryInput = input;
      // Check if each character in the string has an SJIS equivalent. If it does,
      // replace it. A for loop is used so we can peek ahead and determine if we need
      // to halt processing for a period of time, to accomodate for the ASCII file paths
      // used in the EO5/EON voice control code.
      for (int characterIndex = 0; characterIndex < input.Length; characterIndex += 1)
      {
        char character = input[characterIndex];
        // Peek ahead and see if the upcoming bytes are equal to the EO5/EON voice control code.
        //if (characterIndex <= (input.Length -))
        if (SJISTables.ToFullwidth.ContainsKey(character))
        {
          temporaryInput = temporaryInput.Replace(character, SJISTables.ToFullwidth[character]);
        }
      }
      // Swap the temporary buffer back into the primary string.
      input = temporaryInput.Replace("[", "").Replace("]", "");
      return input;
    }
  }
}
