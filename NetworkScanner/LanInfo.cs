using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetworkScanner
{
    public class LanInfo
    {
        #region Member


        #endregion Member

        #region Properties
        public string Name { get; set; }

        public string AdapterDescription { get; set; }

        public IPAddress Address { get; set; }

        #endregion Properties

        #region Constructor
        /// <summary>
        /// Empty constructor for LanInfo
        /// </summary>
        public LanInfo(string name, string description, IPAddress address)
        {
            Name = name;
            AdapterDescription = description;
            Address = address;
        }

        #endregion Constructor

        #region Services
        public string GetInfo()
        {
            //return String.Format("{0} {1} [{2}]", Name, Address, AdapterDescription);
            return String.Format("{0,-16} [{1}]", Address, AdapterDescription);
        }

        #endregion Services

        #region Internal services


        #endregion Internal services

        #region Events


        #endregion Events
    }
}
