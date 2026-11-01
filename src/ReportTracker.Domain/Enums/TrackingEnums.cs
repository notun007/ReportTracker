namespace ReportTracker.Domain.Enums;

public enum ObligationType { Internal = 1, External = 2, InternalCorrection = 3, ExternalCorrection = 4 }
public enum SendingStatus { NotSent = 1, PartiallySent = 2, Sent = 3 }
public enum ReceiptStatus { NotRequired = 1, AwaitingConfirmation = 2, ReceiptConfirmed = 3, ReceiptDisputed = 4 }
public enum EvidenceStatus { NotRequired = 1, Missing = 2, Provided = 3, Verified = 4, Rejected = 5 }
public enum CorrectionStatus { None = 1, Open = 2, Closed = 3 }
public enum ExceptionStatus { None = 1, ApprovedNotApplicable = 2, Cancelled = 3 }
public enum ExtensionStatus { Pending = 1, Approved = 2, Rejected = 3, Withdrawn = 4 }
public enum EvidencePurpose { SendingConfirmation = 1, ExtensionPermission = 2 }
public enum Frequency { Daily = 1, Weekly = 2, Monthly = 3, Quarterly = 4, HalfYearly = 5, Yearly = 6, AdHoc = 7 }
public enum ChannelRequirementType { Mandatory = 1, Optional = 2, Alternative = 3 }
