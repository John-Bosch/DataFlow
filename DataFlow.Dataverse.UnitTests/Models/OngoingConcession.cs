namespace DataFlow.Dataverse.UnitTests.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

public class OngoingConcession
{
    [Column("REBCODE")]
    public string RebateCode { get; set; }

    [Column("REBCONSUMER")]
    public long Consumer { get; set; }

    [Column("REFEFFECT")]
    public DateTime EffectiveDate { get; set; }

    [Column("REBCANCELDATE")]
    public DateTime? CancelDate { get; set; }

    [Column("REBEXPIRYDATE")]
    public DateTime? ExpiryDate { get; set; }

    [Column("REBNAME")]
    public string? RebateName { get; set; }

    [Column("REBNUMBER")]
    public string RebateNumber { get; set; }

    [Column("CUS_CUSTYPE")]
    public long CustomerType { get; set; }

    [Column("REBCARDSTDATE")]
    public DateTime? CardStartDate { get; set; }

    [Column("CONTACTID")]
    public string? ContactId { get; set; }

    [Column("CUS_FIRSTNAME")]
    public string? FirstName { get; set; }

    [Column("CUS_LASTNAME")]
    public string? LastName { get; set; }

    [Column("CACCT")]
    public long AccountNumber { get; set; }

    [Column("ACCOUNTID")]
    public string? AccountId { get; set; }

    [Column("VERSION_STAMP")]
    public long VersionStamp { get; set; }
}