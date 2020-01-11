using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

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
      if (System.IO.File.Exists("Resources/EO3PSNT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      var exampleTable = new OriginTablets.Types.Table("Resources/EO3PSNT.tbl", false);
      var knownCorrect = System.IO.File.ReadAllLines("Resources/EO3PSNT.txt");
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
      if (System.IO.File.Exists("Resources/EO3SCT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCT.tbl is missing. Please copy a vanilla EO3 skillcustomtable.tbl to OTTests/Resources.");
      }
      var exampleTable = new OriginTablets.Types.Table("Resources/EO3SCT.tbl", true);
      var knownCorrect = System.IO.File.ReadAllLines("Resources/EO3SCT.txt");
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
      if (System.IO.File.Exists("Resources/EO3PSNT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3PSNT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      var tableObject = new OriginTablets.Types.Table("Resources/EO3PSNT.tbl", false);
      tableObject.WriteToFile("Resources/EO3PSNTTemp.tbl", false);
      var originalBytes = System.IO.File.ReadAllBytes("Resources/EO3PSNT.tbl");
      var newBytes = System.IO.File.ReadAllBytes("Resources/EO3PSNTTemp.tbl");
      Assert.IsTrue(originalBytes.SequenceEqual(newBytes));
      System.IO.File.Delete("Resources/EO3PSNTTemp.tbl");
    }

    /// <summary>
    /// Checks that the result of writing a Table parsed from a vanilla EO3 playerskillnametable.tbl
    /// matches the parsed file. This is to test long pointer tables.
    /// </summary>
    [TestMethod]
    public void CompareWrittenLongPointerTableWithVanillaTable()
    {
      if (System.IO.File.Exists("Resources/EO3SCT.tbl") == false)
      {
        throw new InternalTestFailureException("EO3SCT.tbl is missing. Please copy a vanilla EO3 playerskillnametable.tbl to OTTests/Resources.");
      }
      var tableObject = new OriginTablets.Types.Table("Resources/EO3SCT.tbl", true);
      tableObject.WriteToFile("Resources/EO3SCTTemp.tbl", true);
      var originalBytes = System.IO.File.ReadAllBytes("Resources/EO3SCT.tbl");
      var newBytes = System.IO.File.ReadAllBytes("Resources/EO3SCTTemp.tbl");
      Assert.IsTrue(originalBytes.SequenceEqual(newBytes));
      System.IO.File.Delete("Resources/EO3SCTTemp.tbl");
    }
  }
}
