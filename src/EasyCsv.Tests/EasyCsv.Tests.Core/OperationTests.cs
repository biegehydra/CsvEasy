using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using EasyCsv.Core;
using EasyCsv.Core.Configuration;

namespace EasyCsv.Tests.Core
{
    public class OperationTests
    {
        private class CsvData
        {
         
            public string Header1 { get; set; }
            public string Header2 { get; set; }
        }
        private const string SingleRecordCsv = "header1,header2\nvalue1,value2";
        private static EasyCsvConfiguration DefaultConfig => EasyCsvConfiguration.Instance;

        [Fact]
        public void GetHeaders_ReturnsCorrectHeaders()
        {
            // Arrange
            var easyCsv = new EasyCsv.Core.EasyCsv(SingleRecordCsv, DefaultConfig);

            // Act
            var headers = easyCsv.GetHeaders();

            // Assert
            Assert.NotNull(headers);
            Assert.Equal(2, headers.Count);
            Assert.Contains("header1", headers);
            Assert.Contains("header2", headers);
        }

        [Fact]
        public void ReplaceHeaderRow_ReplacesHeaderSuccessfully()
        {
            // Arrange
            var easyCsv = new EasyCsv.Core.EasyCsv(SingleRecordCsv, DefaultConfig);
            var newHeaders = new List<string> {"newHeader1", "newHeader2"};

            // Act
            easyCsv.ReplaceHeaderRow(newHeaders);
            var updatedHeaders = easyCsv.GetHeaders();

            // Assert
            Assert.NotNull(updatedHeaders);
            Assert.Equal(2, updatedHeaders.Count);
            Assert.Equal(newHeaders, updatedHeaders);
        }

        [Fact]
        public void ReplaceColumn_ReplacesColumnHeaderSuccessfully()
        {
            // Arrange
            var fileContent = "header1,header2\nvalue1,value2";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            easyCsv.ReplaceColumn("header1", "newHeader1");

            // Assert
            var updatedHeaders = easyCsv.GetHeaders();
            Assert.Equal(2, updatedHeaders!.Count);
            Assert.Contains("newHeader1", updatedHeaders);
            Assert.Contains("header2", updatedHeaders);
            Assert.Equal("value1", easyCsv.CsvContent!.First()["newHeader1"]);
            Assert.Equal("value2", easyCsv.CsvContent.First()["header2"]);
        }

        [Fact]
        public void ReplaceColumn_ReplacesColumnHeaderSuccessfullyStrAndBytes()
        {
            // Arrange
            var fileContent = "header1,header2\nvalue1,value2";
            var firstEasyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            firstEasyCsv.Mutate(x => x.ReplaceColumn("header1", "newHeader1"));

            var newCsv = new EasyCsv.Core.EasyCsv(firstEasyCsv.ContentBytes!, DefaultConfig);

            // Assert
            var updatedHeaders = newCsv.GetHeaders();
            Assert.Equal(2, updatedHeaders!.Count);
            Assert.Contains("newHeader1", updatedHeaders);
            Assert.Contains("header2", updatedHeaders);
            Assert.Equal("value1", newCsv.CsvContent!.First()["newHeader1"]);
            Assert.Equal("value2", newCsv.CsvContent.First()["header2"]);
        }

        [Fact]
        public void GiveColumnsDefaultValues_InsertsDefaultValuesSuccessfully()
        {
            // Arrange
            var fileContent = "header1,header2\nvalue1,value2";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);
            var defaultValues = new Dictionary<string, object> {{"header3", "defaultValue"}};

            // Act
            easyCsv.AddColumns(defaultValues);

            // Assert
            var headers = easyCsv.GetHeaders();
            Assert.Equal(3, headers!.Count);
            Assert.Equal("defaultValue", easyCsv.CsvContent!.First()["header3"]);
        }

        [Fact]
        public void AddColumn_InsertsDefaultValueSuccessfully()
        {
            // Arrange
            var fileContent = "header1,header2\nvalue1,value2";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            easyCsv.AddColumn("header3", "defaultValue");

            // Assert
            var headers = easyCsv.GetHeaders();
            Assert.Equal(3, headers!.Count);
            Assert.Equal("defaultValue", easyCsv.CsvContent!.First()["header3"]);

        }

        [Fact]
        public void RemoveColumn_RemovesColumnSuccessfully()
        {
            // Arrange
            var fileContent = "header1,header2,header3\nvalue1,value2,value3";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            easyCsv.RemoveColumn("header2");

            // Assert
            var headers = easyCsv.GetHeaders();
            Assert.Equal(2, headers!.Count);
            Assert.DoesNotContain("header2", headers);
            Assert.False(easyCsv.CsvContent!.First().ContainsKey("header2"));
        }

        [Fact]
        public void RemoveColumns_RemovesColumnsSuccessfully()
        {
            // Arrange
            var fileContent = "header1,header2,header3\nvalue1,value2,value3";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            easyCsv.RemoveColumns(new List<string> { "header2", "header3" });

            // Assert
            var headers = easyCsv.GetHeaders();
            Assert.Single(headers!);
            Assert.DoesNotContain("header2", headers);
            Assert.DoesNotContain("header3", headers);
            Assert.False(easyCsv.CsvContent!.First().ContainsKey("header2"));
            Assert.False(easyCsv.CsvContent.First().ContainsKey("header3"));
        }

        [Fact]
        public void RemoveUnusedHeaders()
        {
            // Arrange
            var fileContent = "header1,header2,header3\nvalue1,value2,";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            // Act
            easyCsv.RemoveUnusedHeaders();

            // Assert
            var headers = easyCsv.GetHeaders();
            Assert.Equal(2, headers!.Count);
            Assert.DoesNotContain("header3", headers);
        }

        [Fact]
        public async Task ToList_GeneratesListSuccessfully()
        {
            var fileContent = "header1,header2,header3\nvalue1,value2,value3";
            var easyCsv = new EasyCsv.Core.EasyCsv(fileContent, DefaultConfig);

            var list = await easyCsv.GetRecordsAsync<CsvData>();

            foreach (var csvData in list)
            {
                Assert.False(HasNullOrEmptyStrings(csvData, out _));
            }
        }

        private static bool HasNullOrEmptyStrings(object obj, out IEnumerable<string> emptyProperties)
        {
            var properties = obj.GetType().GetProperties();
            List<string> emptyProps = new ();
            foreach (PropertyInfo property in properties.Where(x => x.PropertyType == typeof(string)))
            {
                string value = (string)property.GetValue(obj);
                if (string.IsNullOrWhiteSpace(value))
                {
                    emptyProps.Add(property.Name);
                }
            }
            emptyProperties = emptyProps;
            return emptyProps.Count > 0;
        }

        private static IEasyCsv CreateCsvWithSampleData()
        {
            var data = "Header1,Header2\nValue1,Value2\nValue3,Value4";

            return new EasyCsv.Core.EasyCsv(data, DefaultConfig);
        }

        [Fact]
        public void Combine_TwoEasyCsvInstances_CombinesData()
        {
            // Arrange
            var csv1 = CreateCsvWithSampleData();
            var csv2 = CreateCsvWithSampleData();

            // Act
            var result = csv1.Combine(csv2);

            // Assert
            Assert.Equal(4, result.CsvContent!.Count);
            Assert.Equal("Value1", result.CsvContent[0]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[1]["Header1"]);
            Assert.Equal("Value1", result.CsvContent[2]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[3]["Header1"]);
        }

        [Fact]
        public void Combine_TwoEasyCsvInstancesWithMismatchedHeaders_DoesNotCombineData()
        {
            // Arrange
            var csv1 = CreateCsvWithSampleData();
            var data = "Header3,Header4\nValue5,Value6";
            var csv2 = new EasyCsv.Core.EasyCsv(data, DefaultConfig);

            // Act
            var result = csv1.Combine(csv2);

            // Assert
            Assert.Equal(2, result.CsvContent!.Count);
            Assert.Equal("Value1", result.CsvContent[0]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[1]["Header1"]);
        }

        [Fact]
        public void Combine_ListOfEasyCsvInstances_CombinesData()
        {
            // Arrange
            var csv1 = CreateCsvWithSampleData();
            var csv2 = CreateCsvWithSampleData();
            var csv3 = CreateCsvWithSampleData();
            var csvList = new List<IEasyCsv?> { csv1, csv2, csv3 };

            // Act
            var result = csv1.Combine(csvList);

            // Assert
            Assert.Equal(8, result.CsvContent.Count);
            Assert.Equal("Value1", result.CsvContent[0]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[1]["Header1"]);
            Assert.Equal("Value1", result.CsvContent[2]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[3]["Header1"]);
            Assert.Equal("Value1", result.CsvContent[4]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[5]["Header1"]);
            Assert.Equal("Value1", result.CsvContent[6]["Header1"]);
            Assert.Equal("Value3", result.CsvContent[7]["Header1"]);
        }
    }
}