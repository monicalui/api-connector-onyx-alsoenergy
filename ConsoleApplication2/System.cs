using System;
using System.Collections.Generic;
using OSIsoft.AF.PI;
using OSIsoft.AF;
using OSIsoft.AF.Asset;

namespace ConsoleApplication2
{

    public class Data
    {


        public string dictname { get; set; }

        /*Metstation*/
        public PIPoint Sun { get; set; }

        public PIPoint Sun2 { get; set; }
        public PIPoint Temp1 { get; set; }
        public PIPoint CabF { get; set; }

        public PIPoint WindDirection { get; set; }
        public PIPoint WindSpeed { get; set; }
        public PIPoint RH { get; set; }



        public List<AFValue> pointlist_Sun = new List<AFValue>();
        public List<AFValue> pointlist_Sun2 = new List<AFValue>();
        public List<AFValue> pointlist_Temp1 = new List<AFValue>();
        public List<AFValue> pointlist_CabF = new List<AFValue>();
        public List<AFValue> pointlist_WindDirection = new List<AFValue>();
        public List<AFValue> pointlist_WindSpeed = new List<AFValue>();
        public List<AFValue> pointlist_RH = new List<AFValue>();


        /*Meter*/
        public PIPoint KWHnet { get; set; }
        public PIPoint KW { get; set; }
        public PIPoint PowerFactor { get; set; }
        public PIPoint VacA { get; set; } // shared with Inverter
        public PIPoint VacB { get; set; } // shared with Inverter
        public PIPoint VacC { get; set; } // shared with Inverter
        public PIPoint IacA { get; set; } // shared with Inverter
        public PIPoint IacB { get; set; } // shared with Inverter
        public PIPoint IacC { get; set; } // shared with Inverter
        public PIPoint IacN { get; set; }
        public PIPoint KWHrec { get; set; }
        public PIPoint KWHdel { get; set; }


        public List<AFValue> pointlist_KWHnet = new List<AFValue>();
        public List<AFValue> pointlist_KW = new List<AFValue>();
        public List<AFValue> pointlist_PowerFactor = new List<AFValue>();
        public List<AFValue> pointlist_VacA = new List<AFValue>();
        public List<AFValue> pointlist_VacB = new List<AFValue>();
        public List<AFValue> pointlist_VacC = new List<AFValue>();
        public List<AFValue> pointlist_IacA = new List<AFValue>();
        public List<AFValue> pointlist_IacB = new List<AFValue>();
        public List<AFValue> pointlist_IacC = new List<AFValue>();
        public List<AFValue> pointlist_IacN = new List<AFValue>();
        public List<AFValue> pointlist_KWHrec = new List<AFValue>();
        public List<AFValue> pointlist_KWHdel = new List<AFValue>();

        /*Inverter*/
        public PIPoint Iac { get; set; }
        public PIPoint VacAB { get; set; }
        public PIPoint VacBC { get; set; }
        public PIPoint VacCA { get; set; }
        public PIPoint KwAC { get; set; }
        public PIPoint KwhAC { get; set; }
        public PIPoint Idc { get; set; }
        public PIPoint Vdc { get; set; }
        public PIPoint KwDC { get; set; }


        public List<AFValue> pointlist_Iac = new List<AFValue>();
        public List<AFValue> pointlist_VacAB = new List<AFValue>();
        public List<AFValue> pointlist_VacBC = new List<AFValue>();
        public List<AFValue> pointlist_VacCA = new List<AFValue>();
        public List<AFValue> pointlist_KwAC = new List<AFValue>();
        public List<AFValue> pointlist_KwhAC = new List<AFValue>();
        public List<AFValue> pointlist_Idc = new List<AFValue>();
        public List<AFValue> pointlist_Vdc = new List<AFValue>();
        public List<AFValue> pointlist_KwDC = new List<AFValue>();




        public Data()
        {



        }
    }
    }


