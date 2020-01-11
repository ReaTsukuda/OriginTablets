using Microsoft.VisualStudio.TestTools.UnitTesting;
using OriginTablets.Types;
using System.Linq;
using System.IO;

namespace OTTests
{
  [TestClass]
  public class TableTests
  {
    /// <summary>
    /// Checks the results of parsing a vanilla EO3 playerskillnametable.tbl with a known correct output.
    /// </summary>
    [TestMethod]
    public void CompareParsedTableWithKnownCorrectList()
    {
      if (File.Exists("Resources/EO3PSNT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      var exampleTable = new Table("Resources/EO3PSNT.tbl", false);
      var knownCorrect = File.ReadAllLines("Resources/EO3PSNT.txt");
      // If the table and known correct list differ in number of entries, something has gone very wrong.
      Assert.AreEqual(exampleTable.Count, knownCorrect.Length);
      // If the prior check passed, we can iterate based on a numeric index,
      // and make sure every string is the same.
      for (int index = 0; index < exampleTable.Count; index += 1)
      {
        Assert.AreEqual(exampleTable[index], knownCorrect[index]);
      }
    }

    /// <summary>
    /// Checks the results of parsing a vanilla EO3 skillcustomtable.tbl with a known correct output.
    /// This is to test long pointer tables.
    /// </summary>
    [TestMethod]
    public void CompareParsedLongPointerTableWithKnownCorrectList()
    {
      if (File.Exists("Resources/EO3SCT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCT.tbl is missing. Please copy a vanilla EO3 skillcustomtable.tbl to OTTests/Resources.");
      }
      var exampleTable = new Table("Resources/EO3SCT.tbl", true);
      var knownCorrect = File.ReadAllLines("Resources/EO3SCT.txt");
      // If the table and known correct list differ in number of entries, something has gone very wrong.
      Assert.AreEqual(exampleTable.Count, knownCorrect.Length);
      // If the prior check passed, we can iterate based on a numeric index,
      // and make sure every string is the same.
      for (int index = 0; index < exampleTable.Count; index += 1)
      {
        Assert.AreEqual(exampleTable[index], knownCorrect[index]);
      }
    }

    /// <summary>
    /// Checks that the result of writing a Table parsed from a vanilla EO3 playerskillnametable.tbl
    /// matches the parsed file.
    /// </summary>
    [TestMethod]
    public void CompareWrittenTableWithVanillaTable()
    {
      if (File.Exists("Resources/EO3PSNT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      var tableObject = new Table("Resources/EO3PSNT.tbl", false);
      tableObject.WriteToFile("Resources/EO3PSNTTemp.tbl", false);
      var originalBytes = File.ReadAllBytes("Resources/EO3PSNT.tbl");
      var newBytes = File.ReadAllBytes("Resources/EO3PSNTTemp.tbl");
      Assert.IsTrue(originalBytes.SequenceEqual(newBytes));
      File.Delete("Resources/EO3PSNTTemp.tbl");
    }

    /// <summary>
    /// Checks that the result of writing a Table parsed from a vanilla EO3 playerskillnametable.tbl
    /// matches the parsed file. This is to test long pointer tables.
    /// </summary>
    [TestMethod]
    public void CompareWrittenLongPointerTableWithVanillaTable()
    {
      if (File.Exists("Resources/EO3SCT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCT.tbl is missing. Please copy a vanilla EO3 skillcustomtable.tbl to OTTests/Resources.");
      }
      var tableObject = new Table("Resources/EO3SCT.tbl", true);
      tableObject.WriteToFile("Resources/EO3SCTTemp.tbl", true);
      var originalBytes = File.ReadAllBytes("Resources/EO3SCT.tbl");
      var newBytes = File.ReadAllBytes("Resources/EO3SCTTemp.tbl");
      Assert.IsTrue(originalBytes.SequenceEqual(newBytes));
      File.Delete("Resources/EO3SCTTemp.tbl");
    }

    /// <summary>
    /// Checks that the result of writing a Table parsed from a vanilla EO3 playerskillnametable.tbl
    /// and then modifying its string at index 4 to "MODIFIED NAME" results in a file that is identical
    /// to a known working table with the same modification made.
    /// </summary>
    [TestMethod]
    public void ModifyTableThenCompareToKnownWorkingModifiedTable()
    {
      if (File.Exists("Resources/EO3PSNT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      if (File.Exists("Resources/EO3PSNTModified.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNTModified.tbl is missing. Please copy a EO3 playerskillnametable.tbl with the string at index 4 modified to \"MODIFIED NAME\" to OTTests/Resources.");
      }
      var parsedVanillaTable = new Table("Resources/EO3PSNT.tbl", false);
      parsedVanillaTable[4] = "MODIFIED NAME";
      parsedVanillaTable.WriteToFile("Resources/EO3PSNTTemp.tbl", false);
      var testedBytes = File.ReadAllBytes("Resources/EO3PSNTTemp.tbl");
      var knownWorkingBytes = File.ReadAllBytes("Resources/EO3PSNTModified.tbl");
      Assert.IsTrue(testedBytes.SequenceEqual(knownWorkingBytes));
    }

    /// <summary>
    /// Checks that the result of writing a Table parsed from a vanilla EO3 skillcustomtable.tbl
    /// and then modifying its string at index 23 to "MODIFIED LEVEL UP" results in a file that is identical
    /// to a known working table with the same modification made. This is to test long pointer table modifications.
    /// </summary>
    [TestMethod]
    public void ModifyLongPointerTableThenCompareToKnownWorkingModifiedTable()
    {
      if (File.Exists("Resources/EO3SCT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCT.tbl is missing. Please copy a vanilla EO3 skillcustomtable.tbl to OTTests/Resources.");
      }
      if (File.Exists("Resources/EO3SCTModified.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCTModified.tbl is missing. Please copy a EO3 skillcustomtable.tbl with the string at index 23 modified to \"MODIFIED LEVEL UP\" to OTTests/Resources.");
      }
      var parsedVanillaTable = new Table("Resources/EO3SCT.tbl", true);
      parsedVanillaTable[23] = "MODIFIED LEVEL UP";
      parsedVanillaTable.WriteToFile("Resources/EO3SCTTemp.tbl", true);
      var testedBytes = File.ReadAllBytes("Resources/EO3SCTTemp.tbl");
      var knownWorkingBytes = File.ReadAllBytes("Resources/EO3SCTModified.tbl");
      Assert.IsTrue(testedBytes.SequenceEqual(knownWorkingBytes));
    }
  }
}