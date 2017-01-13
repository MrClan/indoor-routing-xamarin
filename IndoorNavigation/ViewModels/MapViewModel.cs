﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;

namespace IndoorNavigation
{
	/// <summary>
	/// Map view model handles all business logic to do with the map navigation and layers
	/// </summary>
	static class MapViewModel
	{
		public static string defaultHomeLocationText = "Set home location";

		/// <summary>
		/// Sets the initial view point based on user settings. 
		/// </summary>
		/// <param name="map">Map.</param>
		internal async static void SetInitialViewPointAsync(Map map)
		{
			// Get initial viewpoint from settings
			double X = 0, Y = 0, WKID = 0, ZoomLevel = 0;

			for (int i = 0; i < AppSettings.CurrentSettings.InitialViewpointCoordinates.Length; i++)
			{
				switch (AppSettings.CurrentSettings.InitialViewpointCoordinates[i].Key)
				{
					case "X":
						X = AppSettings.CurrentSettings.InitialViewpointCoordinates[i].Value;
						break;
					case "Y":
						Y = AppSettings.CurrentSettings.InitialViewpointCoordinates[i].Value;
						break;
					case "WKID":
						WKID = AppSettings.CurrentSettings.InitialViewpointCoordinates[i].Value;
						break;
					case "ZoomLevel":
						ZoomLevel = AppSettings.CurrentSettings.InitialViewpointCoordinates[i].Value;
						break;
					default:
						break;
				}
			}

			// Location based, location services are on
			if (AppSettings.CurrentSettings.IsLocationServicesEnabled)
			{
				MoveToCurrentLocation(map);
			}
			// Home settings, location services are off but user has a home set
			else if (AppSettings.CurrentSettings.HomeLocation != "Set home location")
			{
				// move first to the extent of the map, then to the extent of the home location
				map.InitialViewpoint = new Viewpoint(new MapPoint(X, Y, new SpatialReference(Convert.ToInt32(WKID))), ZoomLevel);
				await MoveToHomeLocationAsync(map);
			}
			// Default setting, Location services are off and user has no home set
			else
			{
				map.InitialViewpoint = new Viewpoint(new MapPoint(X, Y, new SpatialReference(Convert.ToInt32(WKID))), ZoomLevel);
			}

			// Set minimum and maximum scale for the map
			map.MaxScale = AppSettings.CurrentSettings.MapViewMinScale;
			map.MinScale = AppSettings.CurrentSettings.MapViewMaxScale;

		}

		/// <summary>
		/// Moves to current location of the user .
		/// </summary>
		static void MoveToCurrentLocation(Map map)
		{
			//TODO: Implement when current location is available
		}

		/// <summary>
		/// Moves map to home location.
		/// </summary>
		/// <returns>The viewpoint with coordinates for the home location.</returns>
		/// <param name="map">Map.</param>
		internal static async Task<Viewpoint> MoveToHomeLocationAsync(Map map)
		{
			double X = 0, Y = 0, WKID = 0;

			for (int i = 0; i < AppSettings.CurrentSettings.HomeCoordinates.Length; i++)
			{
				switch (AppSettings.CurrentSettings.HomeCoordinates[i].Key)
				{
					case "X":
						X = AppSettings.CurrentSettings.HomeCoordinates[i].Value;
						break;
					case "Y":
						Y = AppSettings.CurrentSettings.HomeCoordinates[i].Value;
						break;
					case "WKID":
						WKID = AppSettings.CurrentSettings.HomeCoordinates[i].Value;
						break;
					default:
						break;
				}
			}

			var viewpoint = new Viewpoint(new MapPoint(X, Y, new SpatialReference((int)WKID)), 150);
			map.InitialViewpoint = viewpoint;

			//TODO: Remove this when no longer needed
			////Run query to get the floor of the selected room
			//var roomsLayer = map.OperationalLayers[AppSettings.currentSettings.RoomsLayerIndex] as FeatureLayer;
			//var roomsTable = roomsLayer.FeatureTable;

			//// Set query parametersin 
			//var queryParams = new QueryParameters()
			//{
			//	ReturnGeometry = true,
			//	WhereClause = string.Format("LONGNAME = '{0}' OR KNOWN_AS_N = '{0}'", AppSettings.currentSettings.HomeLocation)
			//};

			//// Query the feature table 
			//var queryResult = await roomsTable.QueryFeaturesAsync(queryParams);
			//var homeLocation = queryResult.FirstOrDefault();

			var queryResult = await GetFeaturesFromQueryAsync(map, AppSettings.CurrentSettings.HomeLocation);

			var homeLocation = queryResult.FirstOrDefault();
			SetFloorVisibility(true, map, homeLocation.Attributes[AppSettings.CurrentSettings.RoomsLayerFloorColumnName].ToString());

			return viewpoint;
		}

		internal static async Task<FeatureQueryResult> GetFeaturesFromQueryAsync(Map map, string searchString)
		{
			//Run query to get the floor of the selected room
			var roomsLayer = map.OperationalLayers[AppSettings.CurrentSettings.RoomsLayerIndex] as FeatureLayer;
			var roomsTable = roomsLayer.FeatureTable;



			// Set query parametersin 
			var queryParams = new QueryParameters()
			{
				ReturnGeometry = true,
				WhereClause = string.Format(string.Join(" = '{0}' OR ", AppSettings.CurrentSettings.LocatorFields) + " = '{0}'", searchString)
			};

			// Query the feature table 
			var queryResult = await roomsTable.QueryFeaturesAsync(queryParams);
			return queryResult;
		}

		/// <summary>
		/// Gets the floors in visible area.
		/// </summary>
		/// <returns>The floors in visible area.</returns>
		/// <param name="mapView">Map view.</param>
		internal static async Task<string[]> GetFloorsInVisibleAreaAsync(MapView mapView)
		{
			//Run query to get all the polygons in the visible area
			var roomsLayer = mapView.Map.OperationalLayers[AppSettings.CurrentSettings.RoomsLayerIndex] as FeatureLayer;
			var roomsTable = roomsLayer.FeatureTable;

			// Set query parameters
			var queryParams = new QueryParameters()
			{
				ReturnGeometry = false,
				Geometry = mapView.VisibleArea
			};

			// Query the feature table 
			var queryResult = await roomsTable.QueryFeaturesAsync(queryParams);

			// Group by floors to get the distinct list of floors in the table selection
			var distinctFloors = queryResult.GroupBy(g => g.Attributes[AppSettings.CurrentSettings.RoomsLayerFloorColumnName])
			                                .Select(gr => gr.First().Attributes[AppSettings.CurrentSettings.RoomsLayerFloorColumnName]);

			List<string> tableItems = new List<string>();

			foreach (var item in distinctFloors)
			{
				tableItems.Add(item.ToString());
			}

			// Sort list so floors show up in order
			// Depending on the floors in your building, you might need to create a more complex sorting algorithm
			tableItems.Sort();

			return tableItems.ToArray();
		}

		/// <summary>
		/// Changes the visibility of the rooms and walls layers based on floor selected
		/// </summary>
		/// <param name="areLayersOn">If set to <c>true</c> operational layers are turned on</param>
		/// <param name="map">Map.</param>
		/// <param name="selectedFloor">Selected floor.</param>
		internal static void SetFloorVisibility(bool areLayersOn, Map map, string selectedFloor)
		{
			for (int i = 1; i < map.OperationalLayers.Count; i++)
			{
				var featureLayer = map.OperationalLayers[i] as FeatureLayer;
				if (selectedFloor == "")
				{
					// select first floor by default
					featureLayer.DefinitionExpression = string.Format("{0} = '1'", AppSettings.CurrentSettings.RoomsLayerFloorColumnName);
				}
				else
				{
					// select chosen floor
					featureLayer.DefinitionExpression = string.Format("{0} = '{1}'", 
					                                                  AppSettings.CurrentSettings.RoomsLayerFloorColumnName, 
					                                                  selectedFloor);
				}
				map.OperationalLayers[i].IsVisible = areLayersOn;
			}
		}
	}
}