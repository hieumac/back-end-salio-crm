namespace Salio.Domain.Enums;

public enum OrgRole { Owner, Admin, Manager, Sales, Viewer }

public enum DealSource { Inbound, Outbound, Referral, Marketing, Event, Other }

public enum TaskStatus { Pending, InProgress, Done, Canceled }

public enum TaskPriority { Low, Medium, High, Urgent }

public enum DupConfidence { High, Medium, Low }

public enum DupStatus { Pending, Resolved, Ignored }

public enum LibraryNodeType { Folder, File, Document, Note }

public enum LibraryStatus { Draft, Active, Archived }

public enum LibraryRootType { Company, Personal, Shared }

public enum ChatRole { User, Assistant, System, Tool }

public enum AiInsightStatus { Active, Dismissed, Acted, Expired }

public enum AuthProvider { Password, Google, Microsoft, Apple, Saml, Oidc }

public enum MfaType { Totp, Sms, Email, WebAuthn, RecoveryCode }

public enum LoginResult { Success, InvalidCredentials, Locked, MfaRequired, Disabled }

public enum GrantEffect { Allow, Deny }

public enum PermissionScope { Own, Assigned, Team, Any }

public enum TeamRoleType { Lead, Member }

public enum SystemModuleGroup { Dashboard, Crm, Ai, Library, Reports, Settings, System }

public enum FunctionRiskLevel { Low, Medium, High, Critical }
