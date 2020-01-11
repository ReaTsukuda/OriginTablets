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
  }
}
