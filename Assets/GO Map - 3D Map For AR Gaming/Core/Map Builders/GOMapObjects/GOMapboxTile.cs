﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

using GoShared;
using Mapbox.VectorTile;


namespace GoMap
{
    [ExecuteInEditMode]
    public class GOMapboxTile : GOPBFTileAsync
    {


        public override string GetLayersStrings(GOLayer layer)
        {
            return layer.lyr();
        }
        public override string GetPoisStrings()
        {
            return map.pois.lyr();
        }

        public override string GetLabelsStrings()
        {
            return map.labels.lyr();
        }

        public override string GetPoisKindKey()
        {
            return "type";
        }

        public override GOFeature EditLabelData(GOFeature goFeature)
        {

            IDictionary properties = goFeature.properties;

            string labelKey = goFeature.labelsLayer.LanguageKey(goFeature.goTile.mapType);
            if (properties.Contains(labelKey) && !string.IsNullOrEmpty((string)properties[labelKey]))
            {
                goFeature.name = (string)goFeature.properties[labelKey];
            }
            else goFeature.name = (string)goFeature.properties["name"];

            goFeature.kind = GOEnumUtils.MapboxToKind((string)properties["class"]);
            goFeature.y = goFeature.getLayerDefaultY();


            return goFeature;
        }


public override GOFeature EditFeatureData(GOFeature goFeature)
        {
            if (goFeature == null || goFeature.properties == null)
            {
                // Handle the case where goFeature or its properties are null
                return goFeature;
            }

            if (goFeature.goFeatureType == GOFeatureType.Point)
            {
                // Handle point feature specific logic
                goFeature.name = (string)goFeature.properties["name"];
                return goFeature;
            }

            IDictionary properties = goFeature.properties;

            // Verify that the required properties are present
            if (properties.Contains("class"))
            {
                goFeature.kind = GOEnumUtils.MapboxToKind((string)properties["class"]);
                goFeature.name = (string)properties["class"];
            }
            else if (properties.Contains("type"))
            {
                goFeature.kind = GOEnumUtils.MapboxToKind((string)properties["type"]);
                goFeature.name = (string)properties["type"];
            }
            else
            {
                // Handle the case where neither "class" nor "type" is present
                return goFeature;
            }

            if (goFeature.layer != null && goFeature.layer.layerType == GOLayer.GOLayerType.Roads)
            {
                // Handle Roads layer specific logic
                if (properties.Contains("structure") && (string)properties["structure"] == "bridge")
                {
                    ((GORoadFeature)goFeature).isBridge = true;
                    goFeature.kind = GOFeatureKind.bridge;
                }

                if (properties.Contains("structure") && (string)properties["structure"] == "tunnel")
                {
                    ((GORoadFeature)goFeature).isTunnel = true;
                    goFeature.kind = GOFeatureKind.tunnel;
                }

                if (properties.Contains("structure") && (string)properties["structure"] == "link")
                {
                    ((GORoadFeature)goFeature).isLink = true;
                    // You may want to set a different kind for links, or handle it based on your requirements
                }

                // Fix for v8 streetnames
                if (properties.Contains("name") && !string.IsNullOrEmpty((string)properties["name"]))
                {
                    goFeature.name = (string)properties["name"];
                }
                else
                {
                    goFeature.name = null;
                }
            }

            // Additional logic for height, extrude, etc.
            bool extrude = properties.Contains("extrude") && (string)properties["extrude"] == "true";

            if (goFeature.layer.useRealHeight && properties.Contains("height") && extrude)
            {
                double h = Convert.ToDouble(properties["height"]);
                goFeature.height = (float)h;
            }

            if (goFeature.layer.useRealHeight && properties.Contains("min_height") && extrude)
            {
                double minHeight = Convert.ToDouble(properties["min_height"]);
                goFeature.y = (float)minHeight;
                goFeature.height = (float)goFeature.height - (float)minHeight;
            }

            if (goFeature.layer.forceMinHeight && goFeature.height < goFeature.renderingOptions.polygonHeight && goFeature.y < 0.5f)
            {
                goFeature.height = goFeature.renderingOptions.polygonHeight;
            }

            goFeature.y = (1 + goFeature.layerIndex + goFeature.featureIndex / goFeature.featureCount) / 20f; // Adjusted fraction value

            if (goFeature.renderingOptions != null)
            {
                goFeature.setRenderingOptions();
            }


            return goFeature;
        }


        #region NETWORK

        public override string vectorUrl ()
		{
			var baseUrl = "https://api.mapbox.com:443/v4/";
            var tilesetID = "mapbox.mapbox-streets-v8"; //v7 

            if (map != null && map.customMapboxTilesets != null)
            {
                foreach (GOTilesetLayer tileSet in map.customMapboxTilesets)
                {
                    tilesetID += ("," + tileSet.TilesetID);
                    //tilesetID += (tileSet.TilesetID);
                }
            }
            tilesetID += "/";


            var extension = ".vector.pbf";

			//Download vector data
			Vector2 realPos = goTile.tileCoordinates;
            var tileurl = goTile.zoomLevel + "/" + realPos.x + "/" + realPos.y;

			var completeUrl = baseUrl + tilesetID + tileurl + extension; 
//			var filename = "[MapboxVector]" + gameObject.name;

            if (goTile.apiKey != null && goTile.apiKey!= "") {
                string u = completeUrl + "?access_token=" + goTile.apiKey;
				completeUrl = u;
			}

			return completeUrl;
		}
			
		public override string demUrl ()
		{

			Vector2 realPos = goTile.tileCoordinates;
            var tileurl = goTile.zoomLevel + "/" + realPos.x + "/" + realPos.y;
			var baseUrl = "https://api.mapbox.com/v4/mapbox.terrain-rgb/";
			var extension = ".pngraw";
			var completeUrl = baseUrl + tileurl + extension; 
            if (goTile.apiKey != null && goTile.apiKey != "") {
                string u = completeUrl + "?access_token=" + goTile.apiKey;
				completeUrl = u;
			}

			return completeUrl;
		}

		public override string normalsUrl ()
		{
			return null;
		}

//		public override string normalsUrl ()
//		{
//			//Normals data
//			var tileurl = map.zoomLevel + "/" + goTile.tileCoordinates.x + "/" + goTile.tileCoordinates.y;
//			var baseUrlNormals = "https://tile.mapzen.com/mapzen/terrain/v1/normal/";
//			var extension = ".png";
//			var normalsUrl = baseUrlNormals + tileurl + extension; 
//
//			if (map.mapzen_api_key != null && map.mapzen_api_key != "") {
//				string u = normalsUrl + "?api_key=" + map.mapzen_api_key;
//				normalsUrl = u;
//			}
//
//			return normalsUrl;
//		}

		public override string satelliteUrl (Vector2? tileCoords = null)
		{
            //			//Satellite data
            //            var tileurl = goTile.tileCenter.longitude + "," + goTile.tileCenter.latitude + "," +goTile.zoomLevel;
            //			if (tileCoords != null) {
            //                Coordinates coord = new Coordinates ((Vector2)tileCoords,goTile.zoomLevel+1);
            //                tileurl = coord.longitude + "," + coord.latitude + "," +(goTile.zoomLevel+1);
            //			}

            //			var baseUrl = "https://api.mapbox.com/v4/mapbox.satellite/";
            //            var sizeUrl = "/256x256.jpg?access_token="+goTile.apiKey;
            //			var completeurl = baseUrl + tileurl + sizeUrl; 

            ////			https://api.mapbox.com/v4/mapbox.satellite/7.6409912109375,45.9778785705566,15/256x256.jpg?access_token=pk.eyJ1IjoiYWxhbmdyYW50IiwiYSI6ImNpdHdtMXEwdTAwMXozbms5NzBoOGh4djcifQ.SONpcZWMGNpaFk9tCsupaQ

            //			return completeurl;

            //Satellite data (NEW FORMAT)
            var tileurl = goTile.zoomLevel + "/" + goTile.tileCenter.tileCoordinates(goTile.zoomLevel).x + "/" + goTile.tileCenter.tileCoordinates(goTile.zoomLevel).y;
            if (tileCoords != null)
            {
                Coordinates coord = new Coordinates((Vector2)tileCoords, goTile.zoomLevel + 1);
                tileurl = (goTile.zoomLevel+1) + "/" + coord.tileCoordinates(goTile.zoomLevel+1).x + "/" + coord.tileCoordinates(goTile.zoomLevel+1).y;
            }

            var baseUrl = "https://api.mapbox.com/v4/mapbox.satellite/";
            var sizeUrl = "@2x.jpg90?access_token=" + goTile.apiKey;
            var completeurl = baseUrl + tileurl + sizeUrl;

            return completeurl;



        }


		#endregion

	}
}
