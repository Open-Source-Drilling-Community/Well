using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NORCE.Drilling.Well.Model
{
    public struct CountPerDay
    {
        public DateTime Date { get; set; }
        public ulong Count { get; set; }
        /// <summary>
        /// default constructor
        /// </summary>
        public CountPerDay() { }
        /// <summary>
        /// initialization constructor
        /// </summary>
        /// <param name="date"></param>
        /// <param name="count"></param>
        public CountPerDay(DateTime date, ulong count)
        {
            Date = date;
            Count = count;
        }
    }

    public class History
    {
        public List<CountPerDay> Data { get; set; } = new List<CountPerDay>();
        /// <summary>
        /// default constructor
        /// </summary>
        public History()
        {
            if (Data == null)
            {
                Data = new List<CountPerDay>();
            }
        }

        public void Increment()
        {
            if (Data.Count == 0)
            {
                Data.Add(new CountPerDay(DateTime.UtcNow.Date, 1));
            }
            else
            {
                if (Data[Data.Count - 1].Date < DateTime.UtcNow.Date)
                {
                    Data.Add(new CountPerDay(DateTime.UtcNow.Date, 1));
                }
                else
                {
                    Data[Data.Count - 1] = new CountPerDay(Data[Data.Count - 1].Date, Data[Data.Count - 1].Count + 1);
                }
            }
        }
    }
    public class UsageStatisticsWell
    {
        public static readonly string HOME_DIRECTORY = ".." + Path.DirectorySeparatorChar + "home" + Path.DirectorySeparatorChar;

        public DateTime LastSaved { get; set; } = DateTime.MinValue;
        public TimeSpan BackUpInterval { get; set; } = TimeSpan.FromMinutes(5);

        public History GetAllWellIdPerDay { get; set; } = new History();
        public History GetAllWellMetaInfoPerDay { get; set; } = new History();
        public History GetWellByIdPerDay { get; set; } = new History();
        public History GetAllWellPerDay { get; set; } = new History();
        public History PostWellPerDay { get; set; } = new History();
        public History PutWellByIdPerDay { get; set; } = new History();
        public History DeleteWellByIdPerDay { get; set; } = new History();

        private static object lock_ = new object();

        private static UsageStatisticsWell? instance_ = null;

        public static UsageStatisticsWell Instance
        {
            get
            {
                if (instance_ == null)
                {
                    if (File.Exists(HOME_DIRECTORY + "history.json"))
                    {
                        try
                        {
                            string? jsonStr = null;
                            lock (lock_)
                            {
                                using (StreamReader reader = new StreamReader(HOME_DIRECTORY + "history.json"))
                                {
                                    jsonStr = reader.ReadToEnd();
                                }
                                if (!string.IsNullOrEmpty(jsonStr))
                                {
                                    instance_ = JsonSerializer.Deserialize<UsageStatisticsWell>(jsonStr);
                                }
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    if (instance_ == null)
                    {
                        instance_ = new UsageStatisticsWell();
                    }
                }
                return instance_;
            }
        }

        public void IncrementGetAllWellIdPerDay()
        {
            lock (lock_)
            {
                if (GetAllWellIdPerDay == null)
                {
                    GetAllWellIdPerDay = new History();
                }
                GetAllWellIdPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementGetAllWellMetaInfoPerDay()
        {
            lock (lock_)
            {
                if (GetAllWellMetaInfoPerDay == null)
                {
                    GetAllWellMetaInfoPerDay = new History();
                }
                GetAllWellMetaInfoPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementGetWellByIdPerDay()
        {
            lock (lock_)
            {
                if (GetWellByIdPerDay == null)
                {
                    GetWellByIdPerDay = new History();
                }
                GetWellByIdPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementPostWellPerDay()
        {
            lock (lock_)
            {
                if (PostWellPerDay == null)
                {
                    PostWellPerDay = new History();
                }
                PostWellPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementGetAllWellPerDay()
        {
            lock (lock_)
            {
                if (GetAllWellPerDay == null)
                {
                    GetAllWellPerDay = new History();
                }
                GetAllWellPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementPutWellByIdPerDay()
        {
            lock (lock_)
            {
                if (PutWellByIdPerDay == null)
                {
                    PutWellByIdPerDay = new History();
                }
                PutWellByIdPerDay.Increment();
                ManageBackup();
            }
        }
        public void IncrementDeleteWellByIdPerDay()
        {
            lock (lock_)
            {
                if (DeleteWellByIdPerDay == null)
                {
                    DeleteWellByIdPerDay = new History();
                }
                DeleteWellByIdPerDay.Increment();
                ManageBackup();
            }
        }

        private void ManageBackup()
        {
            if (DateTime.UtcNow > LastSaved + BackUpInterval)
            {
                LastSaved = DateTime.UtcNow;
                try
                {
                    string jsonStr = JsonSerializer.Serialize(this);
                    if (!string.IsNullOrEmpty(jsonStr) && Directory.Exists(HOME_DIRECTORY))
                    {
                        using (StreamWriter writer = new StreamWriter(HOME_DIRECTORY + "history.json"))
                        {
                            writer.Write(jsonStr);
                            writer.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}