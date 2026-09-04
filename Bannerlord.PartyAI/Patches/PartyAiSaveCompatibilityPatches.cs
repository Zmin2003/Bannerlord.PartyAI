using Bannerlord.PartyAI.Parties;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.PartyAI.Patches;

internal static class PartyAiSaveCompatibilityPatches
{
    private static Type _memberLoadDataType = null!;
    private static PropertyInfo _memberSaveIdProperty = null!;
    private static PropertyInfo _objectLoadDataProperty = null!;
    private static PropertyInfo _targetProperty = null!;
    private static FieldInfo _localSaveIdField = null!;
    private static FieldInfo _savedMemberTypeField = null!;
    private static FieldInfo _dataField = null!;

    internal static void Apply(Harmony harmony)
    {
        Type variableLoadDataType = AccessTools.TypeByName("TaleWorlds.SaveSystem.Load.VariableLoadData");
        _memberLoadDataType = AccessTools.TypeByName("TaleWorlds.SaveSystem.Load.MemberLoadData");
        Type objectLoadDataType = AccessTools.TypeByName("TaleWorlds.SaveSystem.Load.ObjectLoadData");
        Type memberTypeIdType = AccessTools.TypeByName("TaleWorlds.SaveSystem.Definition.MemberTypeId");

        if (variableLoadDataType == null
            || _memberLoadDataType == null
            || objectLoadDataType == null
            || memberTypeIdType == null)
        {
            return;
        }

        MethodInfo readMethod = AccessTools.Method(variableLoadDataType, "Read");
        _memberSaveIdProperty = AccessTools.Property(variableLoadDataType, "MemberSaveId");
        _objectLoadDataProperty = AccessTools.Property(_memberLoadDataType, "ObjectLoadData");
        _targetProperty = AccessTools.Property(objectLoadDataType, "Target");
        _localSaveIdField = AccessTools.Field(memberTypeIdType, "LocalSaveId");
        _savedMemberTypeField = AccessTools.Field(variableLoadDataType, "<SavedMemberType>k__BackingField");
        _dataField = AccessTools.Field(variableLoadDataType, "<Data>k__BackingField");

        if (readMethod == null
            || _memberSaveIdProperty == null
            || _objectLoadDataProperty == null
            || _targetProperty == null
            || _localSaveIdField == null
            || _savedMemberTypeField == null
            || _dataField == null)
        {
            return;
        }

        harmony.Patch(
            readMethod,
            postfix: new HarmonyMethod(
                typeof(PartyAiSaveCompatibilityPatches),
                nameof(AfterVariableRead)));
    }

    private static void AfterVariableRead(object __instance)
    {
        if (!_memberLoadDataType.IsInstanceOfType(__instance))
        {
            return;
        }

        object? memberSaveId = _memberSaveIdProperty.GetValue(__instance);
        if (memberSaveId is null)
        {
            return;
        }

        int localSaveId = Convert.ToInt32(_localSaveIdField.GetValue(memberSaveId));
        if (localSaveId != 9)
        {
            return;
        }

        object? objectLoadData = _objectLoadDataProperty.GetValue(__instance);
        object? target = objectLoadData == null ? null : _targetProperty.GetValue(objectLoadData);
        object? savedMemberType = _savedMemberTypeField.GetValue(__instance);
        if (target is not PartyProfile
            || !string.Equals(savedMemberType?.ToString(), "CustomStruct", StringComparison.Ordinal))
        {
            return;
        }

        // v1.5.2 wrote this obsolete enum as a missing child struct. Treat it as
        // its harmless default so the rest of the PartyAI settings can load.
        object basicType = Enum.Parse(_savedMemberTypeField.FieldType, "BasicType");
        _savedMemberTypeField.SetValue(__instance, basicType);
        _dataField.SetValue(__instance, MobileParty.PartyObjective.Neutral);
    }
}
