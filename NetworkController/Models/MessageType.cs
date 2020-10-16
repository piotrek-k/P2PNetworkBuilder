﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Models
{
    public enum MessageType : int
    {
        Unknown = -1,
        Ping = 0,
        PingResponse = 1,
        ReceiveAcknowledge = 2, // information that message has been delivered
        Shutdown = 3,           // gracefully shutdown connection
        Restart = 4,            // reopen connection after shutdown

        // Handshake
        PublicKey = 5,
        PrivateKey = 6,
        AdditionalInfo = 7,
        AdditionalInfoRequest = 8,
        ResetRequest = 9,           // use when encryption keys are lost

        // Network building
        HolePunchingRequest = 10,
        HolePunchingResponse = 11,

        ConnectionRestoreRequest = 12,   // use when you've got encryption keys but have to sync message counters
        ConnectionRestoreResponse = 13
    }
}
