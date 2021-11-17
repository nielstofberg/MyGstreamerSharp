using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Video
{

    public enum RecordOptions
    {
        NoRecord =      0b00000,
        /// <summary>
        /// H264 Encoder (Software encoding only)
        /// </summary>
        H264 =          0b00011,
        /// <summary>
        /// Motion JPEG encoder.
        /// </summary>
        MJPeg =         0b00101,
        /// <summary>
        /// Transfer Stream File. Still works if recording gets interrupted
        /// </summary>
        TSFile =        0b01001,

        /// <summary>
        /// Break file into multiple parts
        /// </summary>
        Multifile =     0b10001,
    }
}
