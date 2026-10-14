using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {
    [AttributeUsage(AttributeTargets.Field)]
    public class HelpBoxAttribute : PropertyAttribute {
        public HelpBoxMessageType MessageType { get; }
        public string Message { get; }

        public HelpBoxAttribute(string message = null, HelpBoxMessageType messageType = HelpBoxMessageType.Info) {
            Message = message;
            MessageType = messageType;
        }
    }
}