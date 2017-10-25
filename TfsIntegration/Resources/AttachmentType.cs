using System;
using System.ComponentModel;
using System.Reflection;

namespace Shared.TfsIntegration.Resources
{
    public enum AttachmentType
    {
        [Description(".png")]
        ScreenshotPng
    }

    public static class AttachmentTypeHelper
    {
        public static string ToDescription(this Enum en)
        {
            string description = string.Empty;

            MemberInfo[] memberInfo = en.GetType().GetMember(en.ToString());

            if (memberInfo.Length > 0)
            {
                var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (null != attributes && attributes.Length > 0)
                    description = !string.IsNullOrWhiteSpace(((DescriptionAttribute)attributes[0]).Description)
                                      ? ((DescriptionAttribute)attributes[0]).Description
                                      : en.ToString();
                else
                    description = en.ToString();
            }
            return description;
        }
    }
}
