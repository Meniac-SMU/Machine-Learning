using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    /// <summary>Selector scores only; these values cannot change player mechanics or the action interface.</summary>
    [Serializable] public sealed class FiveRuleSettings
    {
        public float carry=3,cutbackDeep=3.5f,longPass=2,widePass=2;
        public float contain=4,recover=8,recoverThreat=4,recoverRange=18,recoverSupport=1;
        public float goalBlock=7,singlePressure=3.4f,switchBonus=.45f;
        public void Validate()
        {
            foreach(var field in typeof(FiveRuleSettings).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                var value=(float)field.GetValue(this);
                if(!float.IsFinite(value)||value<0||value>30)throw new ArgumentException("Invalid rule score: "+field.Name);
            }
            if(recoverSupport<1||recoverSupport>4||recoverSupport!=Mathf.Round(recoverSupport))
                throw new ArgumentException("Recovery support must be 1..4 whole players.");
        }
        internal static FiveRuleSettings Load()
        {
            var settings=new FiveRuleSettings();var args=Environment.GetCommandLineArgs();
            var index=Array.IndexOf(args,"--five-rule-settings");
            if(index>=0)
            {
                if(index+1>=args.Length)throw new ArgumentException("Missing rule settings path.");
                JsonUtility.FromJsonOverwrite(File.ReadAllText(args[index+1]),settings);
            }
            settings.Validate();return settings;
        }
    }
}
