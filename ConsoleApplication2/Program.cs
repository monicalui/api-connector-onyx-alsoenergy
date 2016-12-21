using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleApplication2.ServiceReference1;
using System.ServiceModel;
using System.IO;
using OSIsoft.AF.PI;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Search;
using OSIsoft.AF.Time;
using OSIsoft.AF.Data;
using NodaTime;

namespace ConsoleApplication2
{
    public enum AEResultCodes
    {
        OK = 200,
        AuthenticationFailed = 4000,
        SecureConnectionRequired = 4001,
        LoginFailed = 4002,
        NoAPIAccess = 4003,
        NoData = 4004, // Returned when no data is returned from the DB
        AccessDenied = 4005,
        ServerTemporaryUnavailable = 5000,
        ServerError = 5001,
        InvalidInputData = 5002
    }



    class Program
    {
        static string user = "onyxAPI";
        static string password = "Solar2424";
        static int cityofRome = 36566;
        static string PIServer = "pfs1";



        static void Main()
        {
            string sessionId;
            ListResult sites;
      //      List<Data> dataResults = new List<Data>();

            var dict = File.ReadLines(@"C:\also\romemapping.csv").Select(line => line.Split(',')).ToDictionary(line => line[0], line => line[1]);


            PIServer defaultPIServer = GetPIServer(PIServer);

            WebAPIClient api = new WebAPIClient();


            UseHTTPS(api);

            LoginResult login = api.Login(user, password, null);
            sessionId = login.SessionID;
            Console.WriteLine("Session ID:" + sessionId);

            sites = api.GetSiteList(sessionId);
            var hardwares = api.GetSiteHardwareList(sessionId, cityofRome);

            List<DataField> queryFields = new List<DataField>();

            foreach (HardwareComplete h in hardwares.HardwareList)
            {
                queryFields.Clear();

                if (h.FieldList != null && h.FieldList.Count() != 0)
                {



                    Data temp = new Data();



                    if (!dict.ContainsKey(h.HardwareID.ToString()))
                    {
                        continue;
                    }
                    else
                    {
                        string test;
                        dict.TryGetValue(h.HardwareID.ToString(), out test);
                        temp.dictname = test;

                    }



                    InitializePoints(temp, defaultPIServer);



               //     dataResults.Add(temp);


                    foreach (FieldInfo field in h.FieldList)
                    {
                        DataField f = new DataField() { HID = Convert.ToInt32(h.HardwareID), FieldName = field.Name, Function = ConsoleApplication2.ServiceReference1.Functions.Last };
                        queryFields.Add(f);

                        /*** prints to file hardware list
                        System.IO.StreamWriter file = new System.IO.StreamWriter("c:\\also\\listofhardware.txt", true);
                        file.WriteLine(h.HardwareID + " " + h.Name + "\r");

                        file.Close();
                        ***/







                    }



                    try
                    {
                        BinSizes binSize = BinSizes.BinRaw;

                        DateTime startDate = DateTime.UtcNow;
                        DateTime endDate = startDate.AddMinutes(-30);

                        //Backfill
                    //    endDate = endDate.AddDays(-30);

                        TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        DateTime easternStartTime = TimeZoneInfo.ConvertTimeFromUtc(startDate, easternZone);
                        DateTime easternEndTime = TimeZoneInfo.ConvertTimeFromUtc(endDate, easternZone);

                        string startTime = easternStartTime.ToString("yyyy-MM-dd") + "T" + easternStartTime.ToString("HH:mm:ss");
                        string endTime = easternEndTime.ToString("yyyy-MM-dd") + "T" + easternEndTime.ToString("HH:mm:ss");

                        DataField[] ArrayFields = queryFields.ToArray();

                        DataResult result = api.GetBinData(sessionId, endTime, startTime, binSize, ArrayFields);
                        if ((AEResultCodes)result.Code == AEResultCodes.OK)
                        {
                            Console.WriteLine(string.Format("*** GetBinData returned {0} rows", result.DataSet.Count()));
                            for (int i = 0; i < result.DataSet.Count(); i++)
                            {
                                DataBin db = result.DataSet[i];


                                db.Timestamp = db.Timestamp.Substring(0, db.Timestamp.Length - 1);
                                DateTime timestamp = Convert.ToDateTime(db.Timestamp);
                                DateTime timestampUTC = TimeZoneInfo.ConvertTimeToUtc(timestamp, easternZone);


                                GrabData(timestampUTC,ArrayFields, db, temp);

                                

                                Console.WriteLine(string.Format("{0}: {1}   {2}", i, db.Timestamp, string.Join(", ", (from z in db.Data select string.Format("{0:0.000}", z)))));
                            }


                            SendData(temp);


                        }
                        else
                            Console.WriteLine("Error processing query. " + (AEResultCodes)result.Code + " Hardware ID: " + h.HardwareID + " Size: " + ArrayFields.Length);



                    }
                    catch (CommunicationException)
                    {
                        Console.WriteLine("GetBinData failed. Returned data may exceed allowable size. Try reducing the timespan on the query.");
                    }
                    catch
                    {
                        Console.WriteLine("GetBinData failed. Please check your connection.");
                    }


                }

             

            }






            // Always close the client.
            api.Close();
        }


        private static void UseHTTPS(WebAPIClient api)
        { // Set the end point to use SSL
            if (api == null) return;
            BasicHttpBinding binding = (BasicHttpBinding)api.Endpoint.Binding;

            api.Endpoint.Address = new EndpointAddress("https://www.alsoenergy.com/WebAPI/WebAPI.svc");
            binding.Security.Mode = BasicHttpSecurityMode.Transport;

            binding.SendTimeout =
            binding.ReceiveTimeout = new TimeSpan(0, 30, 0);
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferSize = int.MaxValue;
        }




        public static void GrabData(DateTime timestamp, DataField[] ArrayFields, DataBin db, Data temp)
        {
            AFValue newVal;

            if (temp.dictname.Contains("Metstation"))
            {
                //var newVal = new AFValue(valuePair.Value, new AFTime(energyDailyTemp));

                for (int i=0; i < ArrayFields.Length; i++)
                {
                    if (ArrayFields[i].FieldName.Equals("Sun2"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Sun2.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("Sun"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Sun.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("Temp1"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Temp1.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("CabF"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_CabF.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("WindDirection"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_WindDirection.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("WindSpeed"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_WindSpeed.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("RH"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_RH.Add(newVal);
                    }


                }


            }
            else if (temp.dictname.Contains("Meter"))
            {

              
                for (int i = 0; i < ArrayFields.Length; i++)
                {
                    if (ArrayFields[i].FieldName.Equals("KWHnet"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KWHnet.Add(newVal);
                    }
                    if (ArrayFields[i].FieldName.Equals("KW"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KW.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("PowerFactor"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_PowerFactor.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacA"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacA.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacB"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacB.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacA"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacA.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacB"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacB.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacN"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacN.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("KWHrec"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KWHrec.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("KWHdel"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KWHdel.Add(newVal);
                    }


                }
            }
            else
            {
                for (int i = 0; i < ArrayFields.Length; i++)
                {
                    if (ArrayFields[i].FieldName.Equals("VacA"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacA.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacB"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacB.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacA"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacA.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacB"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacB.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("IacC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_IacC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("Iac"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Iac.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacAB"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacAB.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacBC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacBC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("VacCA"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_VacCA.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("KwAC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KwAC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("KwhAC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KwhAC.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("Idc"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Idc.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("Vdc"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_Vdc.Add(newVal);
                    }
                    else if (ArrayFields[i].FieldName.Equals("KwDC"))
                    {
                        newVal = new AFValue(db.Data[i], new AFTime(timestamp));
                        temp.pointlist_KwDC.Add(newVal);
                    }
                }




            }
        }



        public static void SendData(Data temp)
        {
            if (temp.dictname.Contains("Metstation"))
            {

                try
                {
                    AFErrors<AFValue> errors = temp.Sun.UpdateValues(temp.pointlist_Sun, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.Sun2.UpdateValues(temp.pointlist_Sun2, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.Temp1.UpdateValues(temp.pointlist_Temp1, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.CabF.UpdateValues(temp.pointlist_CabF, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.WindDirection.UpdateValues(temp.pointlist_WindDirection, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.WindSpeed.UpdateValues(temp.pointlist_WindSpeed, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }



                try
                {
                    AFErrors<AFValue> errors = temp.RH.UpdateValues(temp.pointlist_RH, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
            else if (temp.dictname.Contains("Meter"))
            {
           
                try
                {
                    AFErrors<AFValue> errors = temp.KWHnet.UpdateValues(temp.pointlist_KWHnet, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.KW.UpdateValues(temp.pointlist_KW, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.PowerFactor.UpdateValues(temp.pointlist_PowerFactor, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacA.UpdateValues(temp.pointlist_VacA, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacB.UpdateValues(temp.pointlist_VacB, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacC.UpdateValues(temp.pointlist_VacC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.IacA.UpdateValues(temp.pointlist_IacA, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.IacB.UpdateValues(temp.pointlist_IacB, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.IacC.UpdateValues(temp.pointlist_IacC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.IacN.UpdateValues(temp.pointlist_IacN, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.KWHrec.UpdateValues(temp.pointlist_KWHrec, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.KWHdel.UpdateValues(temp.pointlist_KWHdel, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
            else
            {
                try
                {
                    AFErrors<AFValue> errors = temp.Iac.UpdateValues(temp.pointlist_Iac, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacAB.UpdateValues(temp.pointlist_VacAB, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacBC.UpdateValues(temp.pointlist_VacBC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacCA.UpdateValues(temp.pointlist_VacCA, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.KwAC.UpdateValues(temp.pointlist_KwAC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                try
                {
                    AFErrors<AFValue> errors = temp.KwhAC.UpdateValues(temp.pointlist_KwhAC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.Idc.UpdateValues(temp.pointlist_Idc, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.Vdc.UpdateValues(temp.pointlist_Vdc, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.KwDC.UpdateValues(temp.pointlist_KwDC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }






                try
                {
                    AFErrors<AFValue> errors = temp.VacA.UpdateValues(temp.pointlist_VacA, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacB.UpdateValues(temp.pointlist_VacB, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.VacC.UpdateValues(temp.pointlist_VacC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }


                try
                {
                    AFErrors<AFValue> errors = temp.IacA.UpdateValues(temp.pointlist_IacA, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.IacB.UpdateValues(temp.pointlist_IacB, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                try
                {
                    AFErrors<AFValue> errors = temp.IacC.UpdateValues(temp.pointlist_IacC, AFUpdateOption.Replace);
                    if (errors != null && errors.Errors.Count > 0)
                    {
                        foreach (var item in errors.Errors)
                        {
                            Console.WriteLine("Timestamp:'{0}', Issue:{1}, System:{2}", item.Key.Timestamp, item.Value.Message, temp.dictname);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }




        public static PIServer GetPIServer(string PIServerName)// Initialization code
        {
            PIServers myPIServers = new PIServers();
            PIServer myPIServer = null;
            try
            {
                myPIServer = myPIServers[PIServerName];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (myPIServer == null)
            {
                Console.WriteLine("Could not find PI Server {0}", PIServerName);
            }
            return myPIServer;
        }


        public static void InitializePoints(Data temp, PIServer defaultPIServer)
        {

            Dictionary<string, object> attributeDefinitions = new Dictionary<string, object>();
            attributeDefinitions.Add("displaydigits", -5);
            attributeDefinitions.Add("ptclassname", "classic");
            attributeDefinitions.Add("descriptor", "This PI Point was automatically created by the onyx application.");
            attributeDefinitions.Add("pointsource", "ONYX-Rome");
            attributeDefinitions.Add("pointtype", PIPointType.Float32);
            attributeDefinitions.Add("archiving", 1);
            attributeDefinitions.Add("compressing", 0);
            attributeDefinitions.Add("compdev", 0);
            attributeDefinitions.Add("compmax", 0);
            attributeDefinitions.Add("compmin", 0);
            attributeDefinitions.Add("compdevpercent", 0);
            attributeDefinitions.Add("engunits", string.Empty);
            attributeDefinitions.Add("excdev", 0);
            attributeDefinitions.Add("excmax", 0);
            attributeDefinitions.Add("excmin", 0);
            attributeDefinitions.Add("excdevpercent", 0);
            attributeDefinitions.Add("instrumenttag", string.Empty);
            attributeDefinitions.Add("scan", 1);
            attributeDefinitions.Add("shutdown", 1);
            attributeDefinitions.Add("span", 100);
            attributeDefinitions.Add("step", 0);
            attributeDefinitions.Add("typicalvalue", 50);
            attributeDefinitions.Add("zero", 0);
            attributeDefinitions.Add("convers", 1);
            attributeDefinitions.Add("filtercode", 0);
            attributeDefinitions.Add("location1", 0);
            attributeDefinitions.Add("location2", 0);
            attributeDefinitions.Add("location3", 0);
            attributeDefinitions.Add("location4", 0);
            attributeDefinitions.Add("location5", 1);
            attributeDefinitions.Add("squareroot", 0);
            attributeDefinitions.Add("srcptid", 0);
            attributeDefinitions.Add("totalcode", 0);
            attributeDefinitions.Add("userint1", 0);
            attributeDefinitions.Add("userint2", 0);
            attributeDefinitions.Add("userreal1", 0);
            attributeDefinitions.Add("userreal2", 0);
            attributeDefinitions.Add("datasecurity", "piadmin: A(r, w) | piadmins: A(r, w) | PowerFactorsAdmins:  A(r, w) | PowerFactorsEmployees:  A(r) | Onyx_R: A(r)");
            attributeDefinitions.Add("ptsecurity", "piadmin: A(r, w) | piadmins: A(r, w) | PowerFactorsAdmins:  A(r, w) | PowerFactorsEmployees:  A(r) | Onyx_R: A(r)");

            if (temp.dictname.Contains("Metstation"))
            {


                if (temp.Sun == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".POA";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Sun = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.Sun2 == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".GHI";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Sun2 = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }





                if (temp.Temp1 == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Ambient Temp";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Temp1 = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.CabF == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".CabF";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.CabF = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.WindDirection == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".WindDirection";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.WindDirection = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.WindSpeed == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".WindSpeed";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.WindSpeed = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.RH == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Relative_Humidity";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.RH = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }
            }
            else if (temp.dictname.Contains("Meter"))
            {


                if (temp.KWHnet == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".KWH.Net";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KWHnet = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.KW == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".AC Power";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KW = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.PowerFactor == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Power Factor";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.PowerFactor = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.VacA == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.A";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacA = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.VacB == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.B";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacB = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.VacC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.C";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.IacA == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.A";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacA = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.IacB == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.B";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacB = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.IacC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.C";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.IacN == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.N";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacN = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.KWHrec == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".KWH.Rec";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KWHrec = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.KWHdel == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".KWH.Del";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KWHdel = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }

            }
            else
            {
                if (temp.VacA == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.A";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacA = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.VacB == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.B";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacB = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.VacC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.C";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.IacA == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.A";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacA = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }




                if (temp.IacB == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.B";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacB = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.IacC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Current.C";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.IacC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.Iac == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".AC Current";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Iac = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.Idc == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".DC Current";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Idc = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.VacAB == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.AB";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacAB = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.VacBC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.BC";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacBC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.VacCA == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".Voltage.CA";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.VacCA = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.Vdc == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".DC Voltage";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.Vdc = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }



                if (temp.KwAC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".AC Power";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KwAC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.KwhAC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".KWH";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KwhAC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }


                if (temp.KwDC == null)
                {
                    //logger.Info("Energy_5min PI Point not found for system {0}.", sys.system_id); //DEBUG

                    bool findPointSuccess_15min = false;
                    string newPointName_15min = "";
                    PIPoint tempPoint_15min;
                    newPointName_15min = temp.dictname + ".DC Power";
                    findPointSuccess_15min = PIPoint.TryFindPIPoint(defaultPIServer, newPointName_15min, out tempPoint_15min); // try to find expected point on PI server
                    if (!findPointSuccess_15min) // if still cannot find the point, create it
                    {
                        Console.WriteLine("Creating PI Point: {0}", newPointName_15min);
                        try
                        {
                            tempPoint_15min = defaultPIServer.CreatePIPoint(newPointName_15min, attributeDefinitions);
                        }
                        catch (AggregateException aex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, aex.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("For system ID {0}: {1}.", temp.dictname, ex.Message);
                        }
                    }
                    if (tempPoint_15min != null)
                    {
                        temp.KwDC = tempPoint_15min;
                    }
                    else
                    {
                        Console.WriteLine("PI Point {0} does not exist.", newPointName_15min);
                    }
                }

            }




        }


    }
   
}