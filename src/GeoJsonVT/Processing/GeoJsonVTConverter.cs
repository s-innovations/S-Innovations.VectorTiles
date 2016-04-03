﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SInnovations.VectorTiles.GeoJsonVT.GeoJson;
using SInnovations.VectorTiles.GeoJsonVT.GeoJson.Geometries;

namespace SInnovations.VectorTiles.GeoJsonVT.Processing
{
    public class GeoJsonVTConverter
    {
        protected GeoJsonVTSimplifier Simplifier { get; private set; }
        public GeoJsonVTConverter(GeoJsonVTSimplifier simplifier = null)
        {
            Simplifier = simplifier ?? new GeoJsonVTSimplifier();
        }
        public List<GeoJsonVTFeature> Convert(GeoJsonObject data, double tolerance)
        {
            var features = new List<GeoJsonVTFeature>();
            if (data.Type == GeoJsonObject.FeatureCollectionType)
            {
                var featureCollection = data as GeoJsonFeatureCollection;

                for (int i = 0; i < featureCollection.Features.Length; i++)
                {
                    ConvertFeature(features, featureCollection.Features[i], tolerance);
                }
            }
            else if (data.Type == GeoJsonObject.FeatureType)
            {
                ConvertFeature(features, data as GeoJsonFeature, tolerance);

            }
            else
            {
                // single geometry or a geometry collection
                ConvertFeature(features, new GeoJsonFeature { Geometry = data as GeoJsonGeometry }, tolerance);
            }

            return features;

        }

        private void ConvertFeature(List<GeoJsonVTFeature> features, GeoJsonFeature feature, double tolerance)
        {
            if (feature.Geometry == null)
                return;

            var geom = feature.Geometry;
            var type = geom.Type;

            if (type == GeoJsonObject.GeoJsonPointType)
            {
                var point = geom as GeoJsonPoint;
                features.Add(Create(feature.Properties, 1, new[] { new GeoJsonVTPointCollection { ProjectPoint(point.Coordinates) } }));
            }
            else if (type == GeoJsonObject.GeoJsonMultiPointType)
            {
                var multiPoint = geom as GeoJsonMultipoint;
                features.Add(Create(feature.Properties, 1, new[] { Project(multiPoint.Coordinates) }));
            }
            else if (type == GeoJsonObject.GeoJsonLineStringType)
            {
                var linestring = geom as GeoJsonLineString;
                features.Add(Create(feature.Properties, 2, new[] { Project(linestring.Coordinates, tolerance) }));

            }
            else if (type == GeoJsonObject.GeoJsonMultiLineStringType || type == GeoJsonObject.GeoJsonPolygonType)
            {
                var coords = (geom as MultiLinetringOrPolygon).Coordinates;
                var rings = new List<GeoJsonVTPointCollection>();
                for (var i = 0; i < coords.Length; i++)
                {
                    rings.Add(Project(coords[i], tolerance));
                }
                features.Add(Create(feature.Properties, type == GeoJsonObject.GeoJsonPolygonType ? 3 : 2, rings.ToArray()));
            }
            else if (type == GeoJsonObject.GeoJsonMultiPolygonType)
            {
                var coords = (geom as GeoJsonMultiPolygon).Coordinates;
                var rings = new List<GeoJsonVTPointCollection>();
                for (var i = 0; i < coords.Length; i++)
                {
                    for (var j = 0; j < coords[i].Length; j++)
                    {
                        rings.Add(Project(coords[i][j], tolerance));
                    }
                }
                features.Add(Create(feature.Properties, 3, rings.ToArray()));

            }
            else if (type == GeoJsonObject.GeoJsonGeometryCollectionType)
            {
                var collection = geom as GeometryCollection;
                for (var i = 0; i < collection.Geometries.Length; i++)
                {
                    ConvertFeature(features, new GeoJsonFeature
                    {
                        Geometry = collection.Geometries[i],
                        Properties = feature.Properties,
                    }, tolerance);
                }

            }
            else
            {
                throw new Exception("Input data is not a valid GeoJSON object.");
            }
        }



        private GeoJsonVTFeature Create(Dictionary<string, object> properties, int v, GeoJsonVTPointCollection[] geoJsonVTPointCollection)
        {
            var feature = new GeoJsonVTFeature
            {
                Geometry = geoJsonVTPointCollection,
                Tags = properties,
                Type = v,


            };
            CalcBBox(feature);
            return feature;
        }
        private GeoJsonVTFeature CalcBBox(GeoJsonVTFeature feature)
        {
            var geometry = feature.Geometry;
            var min = feature.Min;
            var max = feature.Max;

            for (var i = 0; i < geometry.Length; i++)
                CalcRingBBox(min, max, geometry[i]);

            return feature;
        }
        private void CalcRingBBox(double[] min, double[] max, GeoJsonVTPointCollection points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                min[0] = Math.Min(p[0], min[0]);
                max[0] = Math.Max(p[0], max[0]);
                min[1] = Math.Min(p[1], min[1]);
                max[1] = Math.Max(p[1], max[1]);
            }
        }
        private GeoJsonVTPointCollection Project(double[][] lonlats, double? tolerance = null)
        {
            var projected = new GeoJsonVTPointCollection();
            for (var i = 0; i < lonlats.Length; i++)
            {
                projected.Add(ProjectPoint(lonlats[i]));
            }
            if (tolerance.HasValue)
            {
                Simplifier.simplify(projected, tolerance.Value);
                calcSize(projected);
            }
            return projected;
        }
        private void calcSize(GeoJsonVTPointCollection points)
        {
            double area = 0;
            double dist = 0;
            double[] a = null;
            double[] b = null;
            for (int i = 0; i < points.Count - 1; i++)
            {
                a = b ?? points[i];
                b = points[i + 1];

                area += a[0] * b[1] - b[0] * a[1];

                // use Manhattan distance instead of Euclidian one to avoid expensive square root computation
                dist += Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]);
            }
            points.Area = Math.Abs(area / 2);
            points.Distance = dist;
        }
       

        public double[] ProjectPoint(double[] p)
        {
            var sin = Math.Sin(p[1] * Math.PI / 180);
            var x = (p[0] / 360 + 0.5);
            var y = (0.5 - 0.25 * Math.Log((1 + sin) / (1 - sin)) / Math.PI);

            y = y < 0 ? 0 :
                y > 1 ? 1 : y;
            return new[] { x, y, 0.0 };

        }
    }
}
