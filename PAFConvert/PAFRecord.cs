using System;
using CsvHelper.Configuration.Attributes;

namespace PAFConvert
{
    public class PAFRecord
    {
        #nullable enable
        [Index(0)]
        public string Postcode { get; set; }
        [Index(1)]
        public string PostTown { get; set; }
        [Index(2)]
        public string? DependentLocality { get; set; }
        [Index(3)]
        public string? DoubleDependentLocality { get; set; }
        [Index(4)]
        public string? Thoroughfare { get; set; }
        [Index(5)]
        public string? DependentThoroughfare { get; set; }
        [Index(6)]
        public int? BuildingNumber { get; set; }
        [Index(7)]
        public string? BuildingName { get; set; }
        [Index(8)]
        public string? SubBuildingName { get; set; }
        [Index(9)]
        public string? POBox { get; set; }
        [Index(10)]
        public string? DepartmentName { get; set; }
        [Index(11)]
        public string? OrganisationName { get; set; }
        [Index(12)]
        public int UDPRN { get; set; }
        #nullable restore

        public PAFRecord()
        {
        }
    }
}
