﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;

namespace RegulatedNoise.EDDB_Data
{
    
    public class EDMilkyway
    {
        public enum enDataType
        {
            Data_EDDB, 
            Data_Own,
            Data_Merged
        }

        private List<EDSystem>[]        m_Systems;              
        private List<EDStation>[]       m_Stations;
        private List<EDCommoditiesExt>  m_Commodities;

        private bool m_changedSystems = false;
        private bool m_changedStations = false;
            
        // a quick cache for systemlocations
        private Dictionary<string, Point3D> m_cachedLocations = new Dictionary<string, Point3D>();

        /// <summary>
        /// creates a new Milkyway :-)
        /// </summary>
        public EDMilkyway()
        {
            int enumBound = Enum.GetNames(typeof(enDataType)).Length;

            m_Systems       = new List<EDSystem>[enumBound];
            m_Stations      = new List<EDStation>[enumBound];
            m_Commodities   = new List<EDCommoditiesExt>();
        }

        /// <summary>
        /// returns all systems
        /// </summary>
        public List<EDSystem> getSystems(enDataType Systemtype)
        { 
            return m_Systems[(int)Systemtype];
        }

        /// <summary>
        /// returns all stations
        /// </summary>
        public List<EDStation> getStations(enDataType Stationtype)
        { 
            return m_Stations[(int)Stationtype];
        }

        /// <summary>
        /// returns all commodities
        /// </summary>
        public List<EDCommoditiesExt> getCommodities()
        { 
            return m_Commodities;
        }

        /// <summary>
        /// 
        /// </summary>
        public void setCommodities(List<EDCommoditiesExt> newList)
        { 
            m_Commodities.Distinct();
            m_Commodities = newList;

            saveRNCommodityData(@"./Data/commodities_RN.json", true);
        }

        /// <summary>
        /// loads the stationsdata from a file
        /// </summary>
        /// <param name="File">json-file to load</param>
        /// <param name="Stationtype"></param>
        public void loadStationData(string Filename, enDataType Stationtype, bool createNonExistingFile)
        { 
            if (File.Exists(Filename))
                m_Stations[(int)Stationtype]  = JsonConvert.DeserializeObject<List<EDStation>>(File.ReadAllText(Filename));
            else
            { 
                m_Stations[(int)Stationtype] = new List<EDStation>();

                if (createNonExistingFile)
                    saveStationData(Filename, Stationtype, true);
            }
                
        }

        /// <summary>
        /// returns a cloned list of the systems
        /// </summary>
        public List<EDSystem> cloneSystems(enDataType Systemstype)
        { 
            return JsonConvert.DeserializeObject<List<EDSystem>>(JsonConvert.SerializeObject(m_Systems[(int)Systemstype]));
        }

        /// <summary>
        /// returns a cloned list of the stations
        /// </summary>
        public List<EDStation> cloneStations(enDataType Stationtype)
        { 
            return JsonConvert.DeserializeObject<List<EDStation>>(JsonConvert.SerializeObject(m_Stations[(int)Stationtype]));
        }

        /// <summary>
        /// returns a cloned list of the commodities
        /// </summary>
        public List<EDCommoditiesExt> cloneCommodities()
        { 
            return JsonConvert.DeserializeObject<List<EDCommoditiesExt>>(JsonConvert.SerializeObject(m_Commodities));
        }

        /// <summary>
        /// loads the systemsdata from a file
        /// </summary>
        /// <param name="File">json-file to load</param>
        /// <param name="Stationtype"></param>
        public void loadSystemData(string Filename, enDataType Systemtype, bool createNonExistingFile)
        { 
            if (File.Exists(Filename))
                m_Systems[(int)Systemtype]   = JsonConvert.DeserializeObject<List<EDSystem>>(File.ReadAllText(Filename));
            else
            { 
                m_Systems[(int)Systemtype] = new List<EDSystem>();

                if (createNonExistingFile)
                    saveSystemData(Filename, Systemtype, true);
            }
                
        }

        /// <summary>
        /// saves the stationsdata to a file
        /// </summary>
        /// <param name="File">json-file to save</param>
        /// <param name="Stationtype"></param>
        public void saveStationData(string Filename, enDataType Stationtype, bool BackupOldFile)
        { 
            string newFile, backupFile;

            newFile = String.Format("{0}_new{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));
            backupFile = String.Format("{0}_bak{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));

            File.WriteAllText(newFile, JsonConvert.SerializeObject(m_Stations[(int)Stationtype]));
            
            // we delete the current file not until the new file is written without errors

            rotateSaveFiles(Filename, newFile, backupFile, BackupOldFile);
        }

        /// <summary>
        /// loads the systemsdata to a file
        /// </summary>
        /// <param name="File">json-file to save</param>
        /// <param name="Stationtype"></param>
        public void saveSystemData(string Filename, enDataType Systemtype, bool BackupOldFile)
        { 
            string newFile, backupFile;

            newFile = String.Format("{0}_new{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));
            backupFile = String.Format("{0}_bak{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));

            File.WriteAllText(newFile, JsonConvert.SerializeObject(m_Systems[(int)Systemtype]));
            
            // we delete the current file not until the new file is written without errors

            rotateSaveFiles(Filename, newFile, backupFile, BackupOldFile);
        }

        private static void rotateSaveFiles(string Filename, string newFile, string backupFile, bool BackupOldFile)
        {
            // delete old backup
            if (File.Exists(backupFile))
                File.Delete(backupFile);

            // rename current file to old backup
            if (BackupOldFile && File.Exists(Filename))
                File.Move(Filename, backupFile);

            // rename new file to current file
            File.Move(newFile, Filename);
        }

        /// <summary>
        /// merging EDDB and own data to one big list
        /// </summary>
        public bool mergeData()
        {
            int StartingCount;
            bool SystemCanBeDeleted = false;

            //if (false)
            //{ 
            //    // create some data for testing 
            //    if (File.Exists(@".\Data\systems_own.json"))
            //        File.Delete(@".\Data\systems_own.json");
            //    if (File.Exists(@".\Data\stations_own.json"))
            //        File.Delete(@".\Data\stations_own.json");
            //    m_Systems[(int)enDataType.Data_Merged] = new List<EDSystem>(m_Systems[(int)enDataType.Data_EDDB].Where(x => x.Id <= 10));
            //    m_Stations[(int)enDataType.Data_Merged] = new List<EDStation>(m_Stations[(int)enDataType.Data_EDDB].Where(x => x.SystemId <= 10));
            //    saveSystemData(@".\Data\systems_own.json", enDataType.Data_Merged, false);
            //    saveStationData(@".\Data\stations_own.json", enDataType.Data_Merged, false);
            //}
            
            
            // get the base list from EDDB assuming it's the bigger one
            m_Systems[(int)enDataType.Data_Merged] = cloneSystems(enDataType.Data_EDDB);
            m_Stations[(int)enDataType.Data_Merged] = cloneStations(enDataType.Data_EDDB);

            StartingCount =  m_Systems[(int)enDataType.Data_Own].Count;

            for (int i = 0; i < StartingCount; i++)
            {
                SystemCanBeDeleted = true;


                EDSystem ownSystem = m_Systems[(int)enDataType.Data_Own][StartingCount-i-1];
                EDSystem existingEDDNSystem = getSystem(ownSystem.Name);

                if (existingEDDNSystem != null)
                    Debug.Print("Id=" + existingEDDNSystem.Id);
                else
                    Debug.Print("Id=null");

                if (existingEDDNSystem != null)
                {

                    if (existingEDDNSystem.EqualsED(ownSystem))
                    {
                        // systems are equal, check the stations
                        checkStations(ownSystem, existingEDDNSystem, ref SystemCanBeDeleted, ref m_changedStations);
                    }
                    else
                    {
                        // system is existing, but there are differences -> own version has a higher priority
                        SystemCanBeDeleted = false;
                        existingEDDNSystem.getValues(ownSystem);

                        // now check the stations
                        checkStations(ownSystem, existingEDDNSystem, ref SystemCanBeDeleted, ref m_changedStations);

                    }
                }
                else
                {
                    // system is unknown in the EDDB, copy our own system into the merged list
                    SystemCanBeDeleted = false;
                    int newSystemIndex = m_Systems[(int)enDataType.Data_Merged].Max(X => X.Id) + 1;

                    // create system cloneSystems
                    EDSystem newSystem = new EDSystem(newSystemIndex, ownSystem);

                    // add it to merged data
                    m_Systems[(int)enDataType.Data_Merged].Add(newSystem);

                    // now go and get the stations
                    copyStationsForNewSystem(newSystem);
                }

                if (SystemCanBeDeleted)
                {
                    //
                     
                    // delete the system;
                    m_Systems[(int)enDataType.Data_Own].Remove(ownSystem);
                    m_changedSystems = true;

                }
            }

            return changedStations || changedSystems;
        }

        /// <summary>
        /// check if the stations of two stations are equal if the system 
        /// exists in the EDDB data and in the own data
        /// </summary>
        /// <param name="ownSystem">system from own data</param>
        /// <param name="existingEDDNSystem">system from EDDB data</param>
        /// <param name="SystemCanBeDeleted">normally true, except there are differences between at least one stations.
        /// if so the system must be hold as reference. If a own data station is 100% equal (e.g. Commoditynames and other strings
        /// are not casesensitive compared) to the eddb station it will automatically deleted</param>
        private void checkStations(EDSystem ownSystem, EDSystem existingEDDNSystem, ref bool SystemCanBeDeleted, ref bool StationsChanged)
        {
            // own system can be deleted if the stations are equal, too
            List<EDStation> ownSystemStations = getStations(ownSystem.Name, enDataType.Data_Own);

            for (int j = 0; j < ownSystemStations.Count(); j++)
            {
                EDStation ownStation = ownSystemStations[j];
                EDStation existingEDDNStation = getSystemStation(ownSystem.Name, ownSystemStations[j].Name);

                if (existingEDDNStation != null)
                {
                    if (existingEDDNStation.EqualsED(ownStation))
                    {
                        // no favour to hold it anymore
                        m_Stations[(int)enDataType.Data_Own].Remove(ownStation);
                        StationsChanged = true;
                    }
                    else
                    {
                        // copy the values and hold it
                        existingEDDNStation.getValues(ownStation);
                        SystemCanBeDeleted = false;
                    }

                }
                else
                {
                    // station is in EDDB not existing, so we'll hold the station
                    SystemCanBeDeleted = false;
                    int newStationIndex = m_Stations[(int)enDataType.Data_Merged].Max(X => X.Id) + 1;

                    m_Stations[(int)enDataType.Data_Merged].Add(new EDStation(newStationIndex, existingEDDNSystem.Id, ownStation));
                }
            }
        }

        /// <summary>
        /// go and get station clones from the own data for a new station
        /// and adds them to the merged data
        /// </summary>
        /// <param name="newSystem">new created system in merged data </param>
        private void copyStationsForNewSystem(EDSystem newSystem)
        {
            // get the list from own data with the name of the new station 
            // (it's ok because it must be the same name)
            List<EDStation> ownSystemStations = getStations(newSystem.Name, enDataType.Data_Own);
                
            // get the gighest index
            int newStationIndex = m_Stations[(int)enDataType.Data_Merged].Max(X => X.Id);

            for (int j = 0; j < ownSystemStations.Count(); j++)
            {
                newStationIndex++;
                m_Stations[(int)enDataType.Data_Merged].Add(new EDStation(newStationIndex, newSystem.Id, ownSystemStations[j]));
            }
        }

        /// <summary>
        /// get all for stationnames a system
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public string[] getStationNames(string Systemname)
        {
            string[] retValue;

            List<EDStation> StationsInSystem   = getStations(Systemname);
            retValue                            = new string[StationsInSystem.Count];

            for (int i = 0; i < StationsInSystem.Count; i++)
            {
                retValue[i] = StationsInSystem[i].Name;   
            }

            return retValue;
        }

        /// <summary>
        /// get all stations for a system from the main list
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public List<EDStation> getStations(string Systemname)
        {
            return getStations(Systemname, enDataType.Data_Merged);
        }

        /// <summary>
        /// get all stations for a system from a particular list
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public List<EDStation> getStations(string Systemname, enDataType wantedType)
        {
            List<EDStation> retValue;

            EDSystem SystemData = m_Systems[(int)wantedType].Find(x => x.Name==Systemname);

            if (SystemData != null)
                retValue = m_Stations[(int)wantedType].FindAll(x => x.SystemId==SystemData.Id);
            else
                retValue = new List<EDStation>();

            return retValue;
        }

        /// <summary>
        /// check if there's a system with this name
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public bool existSystem(string Systemname)
        {
            return m_Systems[(int)enDataType.Data_Merged].Exists(x => x.Name.Equals(Systemname, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// get the data of a system
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public EDSystem getSystem(string Systemname)
        {
            return m_Systems[(int)enDataType.Data_Merged].Find(x => x.Name.Equals(Systemname, StringComparison.InvariantCultureIgnoreCase));
        }

        private EDStation getSystemStation(string Systemname, string StationName)
        {
            EDStation wantedStation = null;
            EDSystem wantedSystem       = getSystem(Systemname);

            if (wantedSystem != null)
                wantedStation = m_Stations[(int)enDataType.Data_Merged].Find(x => (x.SystemId==wantedSystem.Id) && (x.Name.Equals(StationName)));

            return wantedStation;                
        }

        /// <summary>
        /// get coordinates of a system
        /// </summary>
        /// <param name="Systemname"></param>
        /// <returns></returns>
        public Point3D getSystemCoordinates(string Systemname)
        {   
            Point3D retValue = null;

            if (!String.IsNullOrEmpty(Systemname))
            { 
                if (!m_cachedLocations.TryGetValue(Systemname, out retValue))
                {
                    EDSystem mySystem = m_Systems[(int)enDataType.Data_Merged].Find(x => x.Name.Equals(Systemname, StringComparison.InvariantCultureIgnoreCase));

                    if (mySystem != null)
                    { 
                        retValue = new Point3D((float)mySystem.X, (float)mySystem.Y, (float)mySystem.Z);
                        m_cachedLocations.Add(Systemname, retValue);
                    }

                
                }
            }

            return retValue;
        }

        /// <summary>
        /// returns true if there are some unsaved changes in the own system data
        /// </summary>
        public bool changedSystems
        {
            get 
            {
                return m_changedSystems;
            }
        }

        /// <summary>
        /// returns true if there are some unsaved changes in the own station data
        /// </summary>
        public bool changedStations 
        {
            get 
            {
                return m_changedStations;
            }
        }

        public class MarketData
        { 
            public MarketData()
            {
                Id = -1;
                BuyPrices_Demand    = new List<int>();
                BuyPrices_Supply    = new List<int>();
                SellPrices_Demand   = new List<int>();
                SellPrices_Supply   = new List<int>();
            }


            public int Id;
            public List<int> BuyPrices_Demand;
            public List<int> BuyPrices_Supply;
            public List<int> SellPrices_Demand;
            public List<int> SellPrices_Supply;
        }

        /// <summary>
        /// calculates the min, max and average market price for supply and demand of each commodity
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, MarketData> calculateAveragePrices()
        {
            MarketData CommodityData;
            Dictionary<int, MarketData> collectedData = new Dictionary<int, MarketData>();

            foreach (EDStation Station in m_Stations[(int)(enDataType.Data_Merged)])
            {
                foreach (Listing StationCommodity in Station.Listings)
                {
                    if (!collectedData.TryGetValue(StationCommodity.CommodityId, out CommodityData))
                    {
                        // add a new Marketdata-Object
                        CommodityData = new MarketData();
                        CommodityData.Id        = StationCommodity.CommodityId;
                        collectedData.Add(CommodityData.Id, CommodityData);

                    }

                    if (StationCommodity.Demand != 0)
                    { 
                        if (StationCommodity.BuyPrice > 0)
                            CommodityData.BuyPrices_Demand.Add(StationCommodity.BuyPrice);

                        if (StationCommodity.SellPrice > 0)
                            CommodityData.SellPrices_Demand.Add(StationCommodity.SellPrice);
                        
                    }

                    if (StationCommodity.Supply != 0)
                    { 
                        if (StationCommodity.BuyPrice > 0)
                            CommodityData.BuyPrices_Supply.Add(StationCommodity.BuyPrice);

                        if (StationCommodity.SellPrice > 0)
                            CommodityData.SellPrices_Supply.Add(StationCommodity.SellPrice);
                        
                    }
                }        
            }

            return collectedData;
        }

        /// <summary>
        /// returns the base data of a commodity
        /// </summary>
        /// <param name="CommodityId"></param>
        /// <returns></returns>
        public EDCommoditiesExt getCommodity(string commodityName)
        {
            return m_Commodities.Find(x => x.Name.Equals(commodityName, StringComparison.InvariantCultureIgnoreCase));    
        }

        /// <summary>
        /// returns the base data of a commodity
        /// </summary>
        /// <param name="CommodityId"></param>
        /// <returns></returns>
        public EDCommoditiesExt getCommodity(int CommodityId)
        {
            return m_Commodities.Find(x => x.Id == CommodityId);    
        }

        /// <summary>
        /// calculating the market prices and save them as min and max values for new OCR_ed data,
        /// if "FileName" is not nothing the new data will is saves in this file
        /// 
        /// </summary>
        /// <param name="FileName">name of the file to save to</param>
        public void calculateNewPriceLimits(string FileName = "")
        {
            Dictionary<int, MarketData> collectedData = calculateAveragePrices();

            foreach (MarketData Commodity in collectedData.Values)
            {
                EDCommoditiesExt CommodityBasedata = m_Commodities.Find(x => x.Id == Commodity.Id);

                if (CommodityBasedata != null)
                {
                    if (Commodity.BuyPrices_Demand.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Demand_Buy_Low = Commodity.BuyPrices_Demand.Min();
                    else
                        CommodityBasedata.PriceWarningLevel_Demand_Buy_Low = -1;

                    if (Commodity.BuyPrices_Demand.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Demand_Buy_High = Commodity.BuyPrices_Demand.Max();
                    else
                        CommodityBasedata.PriceWarningLevel_Demand_Buy_High = -1;

                    if (Commodity.BuyPrices_Supply.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Supply_Buy_Low = Commodity.BuyPrices_Supply.Min();
                    else
                        CommodityBasedata.PriceWarningLevel_Supply_Buy_Low = -1;

                    if (Commodity.BuyPrices_Supply.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Supply_Buy_High = Commodity.BuyPrices_Supply.Max();
                    else
                        CommodityBasedata.PriceWarningLevel_Supply_Buy_High = -1;

                    if (Commodity.BuyPrices_Demand.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Demand_Sell_Low = Commodity.SellPrices_Demand.Min();
                    else
                        CommodityBasedata.PriceWarningLevel_Demand_Sell_Low = -1;

                    if (Commodity.SellPrices_Demand.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Demand_Sell_High = Commodity.SellPrices_Demand.Max();
                    else
                        CommodityBasedata.PriceWarningLevel_Demand_Sell_High = -1;

                    if (Commodity.SellPrices_Supply.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Supply_Sell_Low = Commodity.SellPrices_Supply.Min();
                    else
                        CommodityBasedata.PriceWarningLevel_Supply_Sell_Low = -1;

                    if (Commodity.SellPrices_Supply.Count() > 0)
                        CommodityBasedata.PriceWarningLevel_Supply_Sell_High = Commodity.SellPrices_Supply.Max();
                    else
                        CommodityBasedata.PriceWarningLevel_Supply_Sell_High = -1;
                }
                else
                {
                    Debug.Print("STOP");
                }

                //if (CommodityBasedata.Name == "Palladium")
                //    Debug.Print("STOP, doppelt belegt  " + CommodityBasedata.Name);
                //    Debug.Print("STOP");

                //Debug.Print("");
                //Debug.Print("");
                //Debug.Print(CommodityBasedata.Name + " :");
                //Debug.Print("Demand Buy Min \t\t" + Commodity.BuyPrices_Demand.Min().ToString("F0"));
                //Debug.Print("Demand Buy Average\t" + Commodity.BuyPrices_Demand.Average().ToString("F0") + " (" + Commodity.BuyPrices_Demand.Count() + " values)");
                //Debug.Print("Demand Buy Max\t\t" + Commodity.BuyPrices_Demand.Max().ToString("F0"));
                //Debug.Print("");
                //Debug.Print("Demand Sell Min\t\t" + Commodity.SellPrices_Demand.Min().ToString("F0"));
                //Debug.Print("Demand Sell Average\t" + Commodity.SellPrices_Demand.Average().ToString("F0") + " (" + Commodity.SellPrices_Demand.Count() + " values)");
                //Debug.Print("Demand Sell Max\t\t" + Commodity.SellPrices_Demand.Max().ToString("F0"));
                //Debug.Print("");
                //Debug.Print("Supply Buy Min\t\t" + Commodity.BuyPrices_Supply.Min().ToString("F0"));
                //Debug.Print("Supply Buy Average\t" + Commodity.BuyPrices_Supply.Average().ToString("F0") + " (" + Commodity.BuyPrices_Supply.Count() + " values)");
                //Debug.Print("Supply Buy Max\t\t" + Commodity.BuyPrices_Supply.Max().ToString("F0"));
                //Debug.Print("");
                //Debug.Print("Supply Sell Min\t\t" + Commodity.SellPrices_Supply.Min().ToString("F0"));
                //Debug.Print("Supply Sell Average\t" + Commodity.SellPrices_Supply.Average().ToString("F0") + " (" + Commodity.SellPrices_Supply.Count() + " values)");
                //Debug.Print("Supply Sell Max\t\t" + Commodity.SellPrices_Supply.Max().ToString("F0"));
            }

            if (!String.IsNullOrEmpty(FileName))
                saveRNCommodityData(FileName, true);
        }        
        /// <summary>
        /// loads the commodity data from the files
        /// </summary>
        /// <param name="EDDBCommodityDatafile"></param>
        /// <param name="RNCommodityDatafile"></param>
        /// <param name="createNonExistingFile"></param>
       internal void loadCommodityData(string EDDBCommodityDatafile, string RNCommodityDatafile, bool createNonExistingFile)
       {
           bool notExisting = false;
           List<EDCommoditiesWarningLevels> RNCommodities;
           List<EDCommodities> EDDBCommodities = JsonConvert.DeserializeObject<List<EDCommodities>>(File.ReadAllText(EDDBCommodityDatafile));
           

            if (File.Exists(RNCommodityDatafile))
                RNCommodities   = JsonConvert.DeserializeObject<List<EDCommoditiesWarningLevels>>(File.ReadAllText(RNCommodityDatafile));
            else
            {
                notExisting = true;
                RNCommodities   = new List<EDCommoditiesWarningLevels>();
            }

            m_Commodities = EDCommoditiesExt.mergeCommodityData(EDDBCommodities, RNCommodities);

            if (notExisting)            
                calculateNewPriceLimits();

            saveRNCommodityData(RNCommodityDatafile, true);
        }

        /// <summary>
        /// saves the RN-specific commodity data to a file
        /// </summary>
        /// <param name="File">json-file to save</param>
        /// <param name="Stationtype"></param>
        public void saveRNCommodityData(string Filename, bool BackupOldFile)
        {
            List<EDCommoditiesWarningLevels> WarningLevels = EDCommoditiesExt.extractWarningLevels(m_Commodities);

            string newFile, backupFile;


            newFile = String.Format("{0}_new{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));
            backupFile = String.Format("{0}_bak{1}", Path.Combine(Path.GetDirectoryName(Filename), Path.GetFileNameWithoutExtension(Filename)), Path.GetExtension(Filename));

            File.WriteAllText(newFile, JsonConvert.SerializeObject(WarningLevels));
            
            // we delete the current file not until the new file is written without errors

            rotateSaveFiles(Filename, newFile, backupFile, BackupOldFile);
        }

    }
}
