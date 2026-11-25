using UnityEngine;

namespace Capstone.Game.Elements
{
    // Chi gioi han lua chon moi trong Inspector; khong tu doi raw legacy da luu.
    public sealed class SupportedElementAttribute : PropertyAttribute
    {
        public readonly bool AllowUnknown;
        public SupportedElementAttribute(bool allowUnknown = false)
        {
            AllowUnknown = allowUnknown;
        }
    }
}
