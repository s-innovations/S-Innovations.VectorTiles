﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeoJsonVT.Streaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SInnovations.VectorTiles.GeoJsonVT.GeoJson;
using SInnovations.VectorTiles.GeoJsonVT.Models;
using SInnovations.VectorTiles.GeoJsonVT.Models.Converters;

namespace SInnovations.VectorTiles.GeoJsonVT.Tests
{

    public static class GenTiles
    {
        public static Dictionary<string, List<VectorTileFeature>> GenerateTiles(GeoJsonObject data,int maxZoom=14, int maxPoints=100000)
        {
            var index = new GeoJsonVectorTiles();
            index.Options.IndexMaxZoom = maxZoom;
            index.Options.IndexMaxPoints = maxPoints;
            index.ProcessData(data);
            var output = new Dictionary<string, List<VectorTileFeature>>();
            foreach(var id in index.Tiles.TileCoords.Select(k=>k.ToID()))
            {
                var tile = index.Tiles.Get(id);
                var z = (int)Math.Log(tile.Z2,2);
                output[$"z{z}-{tile.X}-{tile.Y}"] = index.GetTile(z, tile.X, tile.Y).Features;
                
            }
            return output;
        }
    }
    [TestClass]
    public class UnitTest1
    {
        static UnitTest1()
        {
            Log.Logger = new LoggerConfiguration()
            .WriteTo
            .LiterateConsole(outputTemplate: "{Timestamp:HH:mm} [{Level}] ({Name:l}){NewLine} {Message}{NewLine}{Exception}")
            .MinimumLevel.Verbose()
            .CreateLogger();
        }
        public static GeoJsonObject Parse(string data)
        {
            return JsonConvert.DeserializeObject<GeoJsonObject>(data, new GeoJsonObjectConverter());

        }
        public static string Load(string name)
        {
           return new StreamReader(
                typeof(UnitTest1).Assembly.GetManifestResourceStream($"SInnovations.VectorTiles.GeoJsonVT.Tests.fixtures.{name}")).ReadToEnd();

        }
        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        [TestMethod]
        public void TestStreaming1()
        {
            var data = Parse(Load("testjson.geojson")) as GeoJsonFeatureCollection;
            var list = new List<VectorTileCoord>();
            var index = new StreamingGeoJsonVectorTiles(new StreamingOptions
            {
                OnNoSingleSplit = (i) =>
                {
                    if(i.ParentSplitCount == 1)
                    {
                        list.Add(i.Coord);
                        foreach(var point in i.Feature.Geometry.First())
                        {
                            Console.WriteLine(string.Join(",", point.Take(2)));
                        }
            
                        foreach(var sub in i.Childs)
                        {
                            Console.WriteLine();
                            foreach (var point in sub.Feature.Geometry.First())
                            {
                                Console.WriteLine(string.Join(",", point.Take(2)));
                            }
                        }
                    }
                }
            });
            index.Tiles = new FileSystemTileStore("./tmp", 5);
            index.Options.Buffer = 0;
            index.TileFaature(data.Features.First());

            index.TileFaature(data.Features.Skip(1).First());

        }
       
        [TestMethod]
        public void TestMethod1()
        {
           

            var data = Parse(Load("testjson.geojson"));
            var index = new GeoJsonVectorTiles();
            index.Options.SolidChildren = true;
          //  index.Options.MaxZoom = maxZoom;
          //  index.Options.IndexMaxPoints = maxPoints;
            index.ProcessData(data);
            var path = new List<VectorTileCoord>();
            var queue = new Queue<VectorTileCoord>();
            queue.Enqueue(new VectorTileCoord());
            while(queue.Count > 0)
            {
                var coord = queue.Dequeue();
                var tile = index.GetTile(coord);
                if (tile != null && coord.Z < index.Options.MaxZoom) 
                {
                    path.Add(coord);
                    foreach (var childcoord in coord.GetChildCoordinate())
                    {
                        queue.Enqueue(childcoord);
                    }
                }
            }
          
            


        }
        [TestMethod]
        public void TestTiles()
        {

            var data = Parse(Load("us-states.json"));
            var index = new GeoJsonVectorTiles();
            index.Options.IndexMaxZoom = 7;
            index.Options.IndexMaxPoints = 7;
            index.ProcessData(data);

           var tile= index.GetTile(7, 37, 48);

          

            var tiles = GenTiles.GenerateTiles(Parse(Load("us-states.json")), 7, 200);
            var json1 = JsonConvert.SerializeObject(tiles["z7-37-48"], new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } });
            Console.WriteLine(json1);
            var json = JsonConvert.SerializeObject(tiles, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } });
          //  File.WriteAllText("c:\\dev\\jsontest.json", json);
            var expected = JObject.Parse(Load("us-states-tiles.json")).ToObject<Dictionary<string,object>>();
           
            Assert.AreEqual(expected.Keys.Count, tiles.Keys.Count);
            foreach(var key in expected.Keys)
            {
              var a = JsonConvert.SerializeObject(expected[key], new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } }); 
              var b= JsonConvert.SerializeObject(tiles[key], new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } }); ;
              Assert.AreEqual(CreateMD5(a), CreateMD5(b));
            }
           //    Assert.AreEqual(CreateMD5(expectedJson), CreateMD5(json));  //Test not wokring due to javascript key ordering different than dotnet

        }
        [TestMethod]
        public void SingleGeomTest()
        {

            var tiles = GenTiles.GenerateTiles(Parse(Load("single-geom.json")));
            var json = JsonConvert.SerializeObject(tiles, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } });
            var expected = Load("single-geom-tiles.json");
            Assert.AreEqual(CreateMD5(expected), CreateMD5(json));

        }

        [TestMethod]
        public void collectionTest()
        {

            var tiles = GenTiles.GenerateTiles(Parse(Load("collection.json")));
            var json = JsonConvert.SerializeObject(tiles, new JsonSerializerSettings {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters= new List<JsonConverter> { new GeoJsonVTFeatureConverter() } });
            var expected = Load("collection-tiles.json");
            Assert.AreEqual(CreateMD5(expected), CreateMD5(json));

        }


        [TestMethod]
        public void datelineTest()
        {

            var tiles = GenTiles.GenerateTiles(Parse(Load("dateline.json")));
            var json = JsonConvert.SerializeObject(tiles, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), Converters = new List<JsonConverter> { new GeoJsonVTFeatureConverter() } });
            var expected = Load("dateline-tiles.json");
            Assert.AreEqual(CreateMD5(expected), CreateMD5(json));

        }

        [TestMethod]
        public void InvalidGeoJson()
        {
            try
            {
                var data = Parse("{\"type\": \"Pologon\"}");
            }
            catch (Exception)
            {
                return;
            }

            Assert.Fail("It should have thrown exception");
        }



        public void TestClip()
        {
        }
    }
}
