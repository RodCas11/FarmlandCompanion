using Microsoft.Data.Sqlite;
using RagnarokCompanion.Services;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class FarmlandLuaTests {
 [Fact] public async Task ServerNamesAndDescriptionsOverrideReferenceWithoutLosingEnglishSearch(){
  string root=Path.Combine(Path.GetTempPath(),"Farmland-lua-"+Guid.NewGuid());Directory.CreateDirectory(root);string database=Path.Combine(root,"items.sqlite"),lua=Path.Combine(root,"farmland.lua");
  try{
   using(var db=new SqliteConnection("Data Source="+database)){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="PRAGMA user_version=2; CREATE TABLE items(id INTEGER PRIMARY KEY,name TEXT NOT NULL,search_name TEXT NOT NULL,name_en TEXT NOT NULL,unidentified_name TEXT NOT NULL,search_en TEXT NOT NULL,search_unidentified TEXT NOT NULL); INSERT INTO items VALUES(19220,'Nome antigo','nome antigo','Gigant Helm','','gigant helm','');";cmd.ExecuteNonQuery();}
   System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
   File.WriteAllText(lua,"tbl = { [19220] = { identifiedDisplayName = \"Elmo Gigante\", unidentifiedDisplayName = \"Equipamento\", identifiedDescriptionName = { \"Bônus do servidor ^0000FFFOR +3^000000\" } }, [19456] = { identifiedDisplayName = \"Máscara do Tengu Corvo [F]\", identifiedDescriptionName = { \"Descrição autorizada.\" } } }",System.Text.Encoding.GetEncoding(1252));
   FarmlandLuaImporter.Import(lua,database);var index=new ItemFarmIndex(database,new());Assert.Equal("Elmo Gigante",index.Find(19220)!.Name);Assert.Equal("Máscara do Tengu Corvo [F]",index.Find(19456)!.Name);Assert.Equal(19220,index.Search("Gigant Helm").Single().Id);
   var description=await new ItemDescriptionService(database).GetAsync(19220);Assert.Contains("FOR +3",description.RawText);Assert.Contains("Farmland",description.Source);
  }finally{SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
 }
}
