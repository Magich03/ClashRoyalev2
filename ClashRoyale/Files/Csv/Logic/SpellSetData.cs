namespace ClashRoyale.Files.Csv.Logic
{
    using System.Collections.Generic;
    using System.Linq;

    public class SpellSetData : CsvData
    {
        public SpellData[] SpellsData;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpellSetData" /> class.
        /// </summary>
        /// <param name="CsvRow">The row.</param>
        /// <param name="CsvTable">The data table.</param>
        public SpellSetData(CsvRow CsvRow, CsvTable CsvTable) : base(CsvRow, CsvTable)
        {
            // SpellSetData.
        }

        public string[] Spells { get; set; }

        /// <summary>
        ///     Called when all instances has been loaded for initialized members in instance.
        /// </summary>
        public override void LoadingFinished()
        {
            this.SpellsData = this.Spells.SelectMany(name => name == "*" ? CsvFiles.Spells : new List<SpellData> { CsvFiles.GetSpellDataByName(name) }).Where(card => card != null && !card.NotInUse).Distinct().ToArray();
        }
    }
}
