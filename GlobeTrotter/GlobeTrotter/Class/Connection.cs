using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace GlobeTrotter
{
    public class Connection
    {
        public static Boolean InternetAccess()
        {
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            return ((profile != null) && (profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess));
        }

        public static Boolean MeteredOrLimited()
        {
            ConnectionCost _connectionCost = NetworkInformation.GetInternetConnectionProfile().GetConnectionCost();
            if ((_connectionCost.Roaming) ||
                (_connectionCost.NetworkCostType == NetworkCostType.Fixed) && (_connectionCost.OverDataLimit || _connectionCost.ApproachingDataLimit))
                return true;
            else
                return false;
        }

        public uint MaxTransferSize(DataPlanStatus _dataPlanStatus)
        {
            return (_dataPlanStatus.MaxTransferSizeInMegabytes.Value);
        }
    }
}
