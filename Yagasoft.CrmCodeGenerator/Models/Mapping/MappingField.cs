﻿#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using Yagasoft.CrmCodeGenerator.Helpers;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Attributes;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Mapping
{
	[Serializable]
	public class MappingField
	{
		public Guid? MetadataId { get; set; }

		public MappingEntity Entity { get; set; }

		public CrmPropertyAttribute Attribute { get; set; }
		public string AttributeOf { get; set; }

		public AttributeTypeCode FieldType { get; set; }
		public string FieldTypeString { get; set; }
		public string TargetTypeForCrmSvcUtil { get; set; }

		public bool IsValidForCreate { get; set; }
		public bool IsValidForRead { get; set; }
		public bool IsValidForUpdate { get; set; }
		public bool IsActivityParty { get; set; }
		public bool IsStateCode { get; set; }
		public bool IsDeprecated { get; set; }
		public string DeprecatedVersion { get; set; }
		private bool IsPrimaryKey { get; set; }

		public bool IsRequired { get; set; }

		public int? MaxLength { get; set; }
		public decimal? Min { get; set; }
		public decimal? Max { get; set; }

		public MappingEnum EnumData { get; set; }
		public MappingImage ImageData { get; set; }
		public MappingLookup LookupData { get; set; }
		
		public string PrivatePropertyName { get; set; }
		public string DisplayName { get; set; }
		public string HybridName { get; set; }
		public string FriendlyName { get; set; }
		public string LogicalName { get; set; }
		public string SchemaName { get; set; }
		public string Label { get; set; }
		public LocalizedLabelSerialisable[] LocalizedLabels { get; set; }

		public string StateName { get; set; }

		public string Description { get; set; }
		public string DescriptionXmlSafe => Naming.XmlEscape(Description);

		public DateTimeBehavior? DateTimeBehavior { get; set; }

		public MappingField()
		{
			IsValidForUpdate = false;
			IsValidForCreate = false;
			IsDeprecated = false;
			Description = "";
		}

		public static void UpdateCache(List<AttributeMetadata> attributeMetadataList, MappingEntity mappingEntity,
			bool isTitleCaseLogicalName)
		{
			// update modified fields
			var modifiedFields =
				mappingEntity.Fields.Where(field => attributeMetadataList.Exists(attMeta => attMeta.MetadataId == field.MetadataId))
					.ToList();
			modifiedFields.AsParallel().ForAll(
				field =>
					GetMappingField(attributeMetadataList.First(attMeta => attMeta.MetadataId == field.MetadataId), mappingEntity,
						field, isTitleCaseLogicalName));

			// add new attributes
			var newAttributes =
				attributeMetadataList.Where(
					attMeta => !Array.Exists(mappingEntity.Fields, field => field.MetadataId == attMeta.MetadataId)).ToList();
			newAttributes.ForEach(
				attMeta =>
				{
					var newFields = new MappingField[mappingEntity.Fields.Length + 1];
					Array.Copy(mappingEntity.Fields, newFields, mappingEntity.Fields.Length);
					mappingEntity.Fields = newFields;
					mappingEntity.Fields[mappingEntity.Fields.Length - 1] = GetMappingField(attMeta, mappingEntity, null,
						isTitleCaseLogicalName);
				});
		}

		public static MappingField GetMappingField(AttributeMetadata attribute, MappingEntity entity,
			MappingField result, bool isTitleCaseLogicalName)
		{
			result = result ?? new MappingField();

			result.Entity = entity;
			result.MetadataId = attribute.MetadataId;
			result.LogicalName = attribute.LogicalName ?? result.LogicalName;
			result.AttributeOf = attribute.AttributeOf ?? result.AttributeOf;
			result.IsValidForCreate = attribute.IsValidForCreate ?? result.IsValidForCreate;
			result.IsValidForRead = attribute.IsValidForRead ?? result.IsValidForRead;
			result.IsValidForUpdate = attribute.IsValidForUpdate ?? result.IsValidForUpdate;

			if (attribute.AttributeType != null)
			{
				result.FieldType = attribute.AttributeType.Value;
				result.IsActivityParty = attribute.AttributeType == AttributeTypeCode.PartyList;
				result.IsStateCode = attribute.AttributeType == AttributeTypeCode.State;
			}

			result.DeprecatedVersion = attribute.DeprecatedVersion ?? result.DeprecatedVersion;

			if (attribute.DeprecatedVersion != null)
			{
				result.IsDeprecated = !string.IsNullOrWhiteSpace(attribute.DeprecatedVersion);
			}

			if (attribute is EnumAttributeMetadata || attribute is BooleanAttributeMetadata)
			{
				result.EnumData = MappingEnum.GetMappingEnum(attribute, result.EnumData, isTitleCaseLogicalName);
			}

			if (attribute is LookupAttributeMetadata lookup)
			{
				result.LookupData = new MappingLookup();

				if (lookup.Targets?.Length == 1)
				{
					result.LookupData.LookupSingleType = lookup.Targets[0];
				}
			}

			ParseDateTime(attribute, result);
			ParseMinMaxValues(attribute, result);
			ParseImageValues(attribute, result);

			result.IsPrimaryKey = attribute.IsPrimaryId ?? result.IsPrimaryKey;

			if (attribute.SchemaName != null)
			{
				result.SchemaName = attribute.SchemaName ?? result.SchemaName;

				if (attribute.LogicalName != null)
				{
					result.DisplayName = Naming.GetProperVariableName(attribute, isTitleCaseLogicalName);
				}

				result.PrivatePropertyName = Naming.GetEntityPropertyPrivateName(attribute.SchemaName);
			}

			result.HybridName = Naming.GetProperHybridFieldName(result.DisplayName, result.Attribute);

			if (attribute.Description?.UserLocalizedLabel != null)
			{
				result.Description = attribute.Description.UserLocalizedLabel.Label;
			}

			if (attribute.DisplayName != null)
			{
				if (attribute.DisplayName.LocalizedLabels != null)
				{
					result.LocalizedLabels = attribute.DisplayName.LocalizedLabels
						.Select(label =>
							new LocalizedLabelSerialisable
							{
								LanguageCode = label.LanguageCode,
								Label = label.Label
							}).ToArray();
				}

				if (attribute.DisplayName.UserLocalizedLabel != null)
				{
					result.Label = attribute.DisplayName.UserLocalizedLabel.Label;
				}
			}

			if (attribute.RequiredLevel != null)
			{
				result.IsRequired = attribute.RequiredLevel.Value == AttributeRequiredLevel.ApplicationRequired;
			}

			if (attribute.AttributeType != null)
			{
				result.Attribute =
					new CrmPropertyAttribute
					{
						LogicalName = attribute.LogicalName,
						IsLookup = attribute.AttributeType == AttributeTypeCode.Lookup
							|| attribute.AttributeType == AttributeTypeCode.Owner
							|| attribute.AttributeType == AttributeTypeCode.Customer,
						IsImage = attribute is ImageAttributeMetadata
					};
				result.Attribute.IsMultiTyped = result.Attribute.IsLookup && result.LookupData?.LookupSingleType.IsEmpty() == true;
			}

			result.TargetTypeForCrmSvcUtil = GetTargetType(result);
			result.FieldTypeString = result.TargetTypeForCrmSvcUtil;

			return result;
		}

		private static void ParseMinMaxValues(AttributeMetadata attribute, MappingField result)
		{
			if (attribute is StringAttributeMetadata)
			{
				var attr = attribute as StringAttributeMetadata;

				result.MaxLength = attr.MaxLength ?? result.MaxLength;
			}

			if (attribute is MemoAttributeMetadata)
			{
				var attr = attribute as MemoAttributeMetadata;

				result.MaxLength = attr.MaxLength ?? result.MaxLength;
			}

			if (attribute is IntegerAttributeMetadata)
			{
				var attr = attribute as IntegerAttributeMetadata;

				result.Min = attr.MinValue ?? result.Min;
				result.Max = attr.MaxValue ?? result.Max;
			}

			if (attribute is DecimalAttributeMetadata)
			{
				var attr = attribute as DecimalAttributeMetadata;

				result.Min = attr.MinValue ?? result.Min;
				result.Max = attr.MaxValue ?? result.Max;
			}

			if (attribute is MoneyAttributeMetadata)
			{
				var attr = attribute as MoneyAttributeMetadata;

				result.Min = attr.MinValue != null ? (decimal) attr.MinValue.Value : result.Min;
				result.Max = attr.MaxValue != null ? (decimal) attr.MaxValue.Value : result.Max;
			}

			if (attribute is DoubleAttributeMetadata)
			{
				var attr = attribute as DoubleAttributeMetadata;

				result.Min = attr.MinValue != null ? (decimal) attr.MinValue.Value : result.Min;
				result.Max = attr.MaxValue != null ? (decimal) attr.MaxValue.Value : result.Max;
			}
		}

		private static void ParseDateTime(AttributeMetadata attribute, MappingField result)
		{
			var metadata = attribute as DateTimeAttributeMetadata;
			var behaviour = metadata?.DateTimeBehavior?.Value;

			if (metadata != null)
			{
				if (result.FieldType == AttributeTypeCode.DateTime
					&& !string.IsNullOrEmpty(behaviour))
				{
					try
					{
						result.DateTimeBehavior = (DateTimeBehavior) Enum.Parse(typeof(DateTimeBehavior), behaviour);
					}
					catch
					{
						// ignored
					}
				}
			}
		}
		
		private static void ParseImageValues(AttributeMetadata attribute, MappingField result)
		{
			if (attribute is ImageAttributeMetadata image)
			{
				result.ImageData =
					new MappingImage
					{
						CanStoreFullImage = image.CanStoreFullImage ?? result.ImageData?.CanStoreFullImage,
						MaxWidth = image.MaxWidth ?? result.ImageData?.MaxWidth,
						MaxHeight = image.MaxHeight ?? result.ImageData?.MaxHeight,
						MaxSizeInKb = image.MaxSizeInKB ?? result.ImageData?.MaxSizeInKb
					};
			}
		}

		private static string GetTargetType(MappingField field)
		{
			if (field.IsPrimaryKey)
			{
				return "Guid?";
			}

			switch (field.FieldType)
			{
				case AttributeTypeCode.Picklist:
					return "OptionSetValue";
				case AttributeTypeCode.BigInt:
					return "long?";
				case AttributeTypeCode.Integer:
					return "int?";
				case AttributeTypeCode.Boolean:
					return "bool?";
				case AttributeTypeCode.DateTime:
					return "DateTime?";
				case AttributeTypeCode.Decimal:
					return "decimal?";
				case AttributeTypeCode.Money:
					return "Money";
				case AttributeTypeCode.Double:
					return "double?";
				case AttributeTypeCode.Uniqueidentifier:
					return "Guid?";
				case AttributeTypeCode.Lookup:
				case AttributeTypeCode.Owner:
				case AttributeTypeCode.Customer:
					return "EntityReference";
				case AttributeTypeCode.State:
					return field.Entity.StateName + "?";
				case AttributeTypeCode.Status:
					return "OptionSetValue";
				case AttributeTypeCode.Memo:
				case AttributeTypeCode.Virtual:
				case AttributeTypeCode.EntityName:
				case AttributeTypeCode.String:
					return "string";
				case AttributeTypeCode.PartyList:
					return "ActivityParty[]";
				case AttributeTypeCode.ManagedProperty:
					return "BooleanManagedProperty";
				default:
					return "object";
			}
		}

		public string GetMethod { get; set; }
	}

	[Serializable]
	public enum DateTimeBehavior
	{
		DateOnly = 2,
		TimeZoneIndependent = 3,
		UserLocal = 1
	}
}
