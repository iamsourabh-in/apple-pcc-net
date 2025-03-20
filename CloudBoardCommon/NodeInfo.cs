using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class NodeInfo
    {
        public string OSVersion { get; set; }
        public string OS { get; set; }
        public string DeviceModel { get; set; }
        public bool? IsLeader { get { return NodeType == NodeTypeEnum.Leader; } }
        public NodeTypeEnum NodeType { get; set; }
        public NodeInfo()
        {
            NodeType = NodeTypeEnum.Leader;
            DeviceModel = "MAC";
            OS = "MAC";
            OSVersion = "1";
        }
        public static NodeInfo Load() {
            return new NodeInfo();
        }
    }

    public enum NodeTypeEnum
    {
        Leader,
        Follower
    }
}
