using System;
using UnityEngine;

namespace GDS.Core {
    [AttributeUsage(AttributeTargets.Field)]
    public class InspectorButtonAttribute : PropertyAttribute {
        public string MethodName { get; }

        public InspectorButtonAttribute(string methodName = null) {
            MethodName = methodName;
        }
    }
}