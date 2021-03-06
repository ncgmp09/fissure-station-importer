﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using ESRI.ArcGIS.Catalog;
using ESRI.ArcGIS.CatalogUI;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;

namespace FissureBar
{
    public class Fishbutton : ESRI.ArcGIS.Desktop.AddIns.Button
    {
        public Fishbutton()
        {
        }

        protected override void OnClick()
        {
            //
            //  TODO: Sample code showing how to access button host
            //
            #region Get Shapefile
            // Ask user to browse to a shapefile
            IGxObject openedFile = commonFunctions.OpenShapefile("Select your Fissure Waypoint Shapefile");
            if (openedFile == null) { return; }

            // Open the file as an IFeatureClass
            IWorkspaceFactory wsFact = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace ws = wsFact.OpenFromFile(openedFile.Parent.FullName, 0) as IFeatureWorkspace;

            string path = @"C:\tmp\config.txt";
            if (!File.Exists(path))
            {
                MessageBox.Show("Missing the config File !");
                return;
            }

            IFeatureClass fissureWaypoints = ws.OpenFeatureClass(openedFile.Name);

            // Make sure user selected a point featureclass
            if (fissureWaypoints.ShapeType != esriGeometryType.esriGeometryPoint)
            {
                MessageBox.Show("The shapefile you selected does not contain points. Try again.");
                return;
            }

            // Make sure that the Coordinate System is set
            IGeoDataset gDs = fissureWaypoints as IGeoDataset;
            IGeoDatasetSchemaEdit schemaEditor = gDs as IGeoDatasetSchemaEdit;
            ISpatialReferenceFactory2 spaRefFact = new SpatialReferenceEnvironmentClass();
            IProjectedCoordinateSystem projCs = spaRefFact.CreateProjectedCoordinateSystem((int)esriSRProjCSType.esriSRProjCS_NAD1983UTM_12N);
            schemaEditor.AlterSpatialReference(projCs);

            // Put all the points into a cursor
            IFeatureCursor sourcePoints = fissureWaypoints.Search(null, false);
            #endregion

            #region Prepare for Loop
            // Get a reference to the Stations featureclass in EarthFissure SDE database
            IWorkspace sdeWs = commonFunctions.OpenFissureWorkspace();

            if (sdeWs == null)
            {
                return;
            }
            IFeatureClass stations = commonFunctions.OpenFeatureClass(sdeWs, "Stations");

            // Get a reference to the Fissure Info table in the SDE database
            ITable fissInfoTable = commonFunctions.OpenTable(sdeWs, "FissureStationDescription");

            // Get a reference to the SysInfo table for spinning up IDs
            sysInfo fissDbInfo = new sysInfo(sdeWs);

            // Get field indexes
            Dictionary<string, int> stationIndexes = GetFieldIndexes(stations as ITable);
            Dictionary<string, int> infoIndexes = GetFieldIndexes(fissInfoTable);
            Dictionary<string, int> sourceIndexes = GetFieldIndexes(fissureWaypoints as ITable);

            // Need a geographic coordinate system in the loop
            IGeographicCoordinateSystem geoCs = spaRefFact.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_NAD1983);

            // Setup the ProgressBar
            //setupProgressBar(fissureWaypoints.FeatureCount(null));
            #endregion

            #region Perform Loop
            // Start an edit session
            IWorkspaceEdit wsEditor = sdeWs as IWorkspaceEdit;
            wsEditor.StartEditing(false);
            wsEditor.StartEditOperation();

            try
            {
                // Get Insert Cursors
                IFeatureCursor stationInsert = stations.Insert(true);
                ICursor stationInfoInsert = fissInfoTable.Insert(true);

                // Loop through the source points, appending appropriately to both tables.
                IFeature sourcePoint = sourcePoints.NextFeature();

                while (sourcePoint != null)
                {
                    // Get the new station's identifier
                    string stationID = "FIS.StationPoints." + fissDbInfo.GetNextIdValue("StationPoints");

                    // Get the new station description entry's identifier
                    string descriptionID = "FIS.FissureStationDescription." + fissDbInfo.GetNextIdValue("FissureStationDescription");

                    // Get the Lat/Long values for the new point
                    IGeometry locGeom = sourcePoint.ShapeCopy as IGeometry;
                    locGeom.Project(geoCs);
                    IPoint locPoint = locGeom as IPoint;

                    // Make the new StationPoint
                    IFeatureBuffer newStation = stations.CreateFeatureBuffer();
                    newStation.set_Value(stationIndexes["Stations_ID"], stationID);
                    newStation.set_Value(stationIndexes["FieldID"], "");
                    newStation.set_Value(stationIndexes["Label"], "");
                    newStation.set_Value(stationIndexes["Symbol"], sourcePoint.get_Value(sourceIndexes["Waypoint_T"]));
                    newStation.set_Value(stationIndexes["PlotAtScale"], 24000);
                    newStation.set_Value(stationIndexes["LocationConfidenceMeters"], sourcePoint.get_Value(sourceIndexes["Horz_Prec"]));
                    newStation.set_Value(stationIndexes["MapY"], locPoint.Y);
                    newStation.set_Value(stationIndexes["MapX"], locPoint.X);
                    newStation.set_Value(stationIndexes["DataSourceID"], "");
                    newStation.Shape = sourcePoint.ShapeCopy;
                    stationInsert.InsertFeature(newStation);

                    // Make the new FissureDescription
                    IRowBuffer newDescription = fissInfoTable.CreateRowBuffer();
                    newDescription.set_Value(infoIndexes["stationid"], stationID);
                    newDescription.set_Value(infoIndexes["dateofobservation"], sourcePoint.get_Value(sourceIndexes["Date_of_th"]));
                    newDescription.set_Value(infoIndexes["timeofobservation"], sourcePoint.get_Value(sourceIndexes["Time_of_th"]));
                    newDescription.set_Value(infoIndexes["surfaceexpression"], sourcePoint.get_Value(sourceIndexes["Surface_Ex"]));
                    newDescription.set_Value(infoIndexes["displacement"], sourcePoint.get_Value(sourceIndexes["Displaceme"]));
                    newDescription.set_Value(infoIndexes["surfacewidth"], sourcePoint.get_Value(sourceIndexes["Surface_Wi"]));
                    newDescription.set_Value(infoIndexes["fissuredepth"], sourcePoint.get_Value(sourceIndexes["Fissure_De"]));
                    newDescription.set_Value(infoIndexes["vegetation"], sourcePoint.get_Value(sourceIndexes["Vegetation"]));
                    newDescription.set_Value(infoIndexes["fissureshape"], sourcePoint.get_Value(sourceIndexes["Fissure_Sh"]));
                    newDescription.set_Value(infoIndexes["linecontinuity"], string.IsNullOrEmpty(sourcePoint.get_Value(sourceIndexes["Line_Conti"]).ToString().Trim()) ? DBNull.Value : sourcePoint.get_Value(sourceIndexes["Line_Conti"]));
                    newDescription.set_Value(infoIndexes["datafile"], sourcePoint.get_Value(sourceIndexes["Datafile"]));
                    newDescription.set_Value(infoIndexes["locationwrtfissure"], sourcePoint.get_Value(sourceIndexes["Location_w"]));
                    newDescription.set_Value(infoIndexes["vegetationtype"], sourcePoint.get_Value(sourceIndexes["Vegetatio2"]));
                    newDescription.set_Value(infoIndexes["fissdescription_id"], descriptionID);
                    stationInfoInsert.InsertRow(newDescription);

                    // Iterate
                    sourcePoint = sourcePoints.NextFeature();
                   // progress.PerformStep();
                }

                // Done. Save edits.
                wsEditor.StopEditOperation();
                wsEditor.StopEditing(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
                wsEditor.StopEditOperation();
                wsEditor.StopEditing(false);
            }
            finally
            {
               // progress.Visible = false;
            }
            #endregion
           
            ArcMap.Application.CurrentTool = null;
        }

       private Dictionary<string, int> GetFieldIndexes(ITable theTable)
       {
           Dictionary<string, int> theResult = new Dictionary<string, int>();
           IFields theFields = theTable.Fields;

           for (int i = 0; i < theFields.FieldCount; i++)
           {
               theResult.Add(theFields.Field[i].Name, i);
           }

           return theResult;
       }
}
}
