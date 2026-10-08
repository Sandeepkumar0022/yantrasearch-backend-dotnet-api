-- Additive schema for Java/React parity. Does not drop or alter existing columns except adding ContactMessage.Phone.

ALTER TABLE ContactMessage ADD COLUMN Phone VARCHAR(50) NULL;

CREATE TABLE IF NOT EXISTS ClientRequirement (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ClientUserId VARCHAR(128) NULL,
  ClientName VARCHAR(255) NULL,
  Title VARCHAR(255) NOT NULL,
  Description LONGTEXT NOT NULL,
  Location VARCHAR(255) NULL,
  Budget VARCHAR(255) NULL,
  Status VARCHAR(32) NOT NULL DEFAULT 'new',
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  UpdatedByUserId VARCHAR(128) NULL,
  INDEX IX_ClientRequirement_ClientUserId (ClientUserId),
  INDEX IX_ClientRequirement_CreatedAt (CreatedAt),
  CONSTRAINT FK_ClientRequirement_Users FOREIGN KEY (ClientUserId) REFERENCES Users (Id)
);

CREATE TABLE IF NOT EXISTS ClientRequirementAttachment (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  RequirementId INT NOT NULL,
  Filename VARCHAR(512) NOT NULL,
  ContentType VARCHAR(255) NULL,
  SizeBytes BIGINT NOT NULL,
  Data LONGBLOB NOT NULL,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_ClientRequirementAttachment_RequirementId (RequirementId),
  CONSTRAINT FK_ClientRequirementAttachment_Req FOREIGN KEY (RequirementId) REFERENCES ClientRequirement (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS MembershipPlan (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Code VARCHAR(64) NOT NULL,
  Name VARCHAR(255) NOT NULL,
  Description LONGTEXT NULL,
  PriceAmount DECIMAL(14,2) NULL,
  Currency VARCHAR(8) NULL,
  BillingPeriod VARCHAR(32) NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  UNIQUE INDEX UX_MembershipPlan_Code (Code)
);

CREATE TABLE IF NOT EXISTS UserMembership (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId VARCHAR(128) NOT NULL,
  CurrentTier VARCHAR(64) NULL,
  PaymentEnabled TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  UNIQUE INDEX UX_UserMembership_UserId (UserId),
  CONSTRAINT FK_UserMembership_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS PaymentOrder (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId VARCHAR(128) NOT NULL,
  MembershipPlanId INT NULL,
  AmountPaise BIGINT NOT NULL,
  Currency VARCHAR(8) NULL,
  Status VARCHAR(32) NOT NULL,
  MetadataJson LONGTEXT NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  INDEX IX_PaymentOrder_UserId (UserId),
  INDEX IX_PaymentOrder_Status (Status),
  CONSTRAINT FK_PaymentOrder_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_PaymentOrder_Plan FOREIGN KEY (MembershipPlanId) REFERENCES MembershipPlan (Id)
);

CREATE TABLE IF NOT EXISTS AppSetting (
  SettingKey VARCHAR(128) NOT NULL PRIMARY KEY,
  Value LONGTEXT NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  UpdatedByUserId VARCHAR(128) NULL
);

CREATE TABLE IF NOT EXISTS SupplierOffering (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  VendorProfileId INT NOT NULL,
  OfferingGroup VARCHAR(64) NOT NULL,
  Category VARCHAR(128) NOT NULL,
  CategoryOther VARCHAR(255) NULL,
  Subcategory VARCHAR(128) NOT NULL,
  SubcategoryOther VARCHAR(255) NULL,
  Note LONGTEXT NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  DeletedAt DATETIME NULL,
  INDEX IX_SupplierOffering_Vendor (VendorProfileId),
  CONSTRAINT FK_SupplierOffering_Vendor FOREIGN KEY (VendorProfileId) REFERENCES VendorProfile (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS JobCategory (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Code VARCHAR(64) NOT NULL,
  Name VARCHAR(255) NOT NULL,
  UNIQUE INDEX UX_JobCategory_Code (Code)
);

CREATE TABLE IF NOT EXISTS Job (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  PostedByUserId VARCHAR(128) NOT NULL,
  JobCategoryId INT NULL,
  Title VARCHAR(512) NOT NULL,
  Description LONGTEXT NULL,
  EmploymentType VARCHAR(32) NULL,
  WorkMode VARCHAR(32) NULL,
  LocationCity VARCHAR(128) NULL,
  LocationState VARCHAR(128) NULL,
  Country VARCHAR(128) NULL,
  SalaryMin DECIMAL(14,2) NULL,
  SalaryMax DECIMAL(14,2) NULL,
  SalaryPeriod VARCHAR(32) NULL,
  Status VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
  PublishedAt DATETIME NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  DeletedAt DATETIME NULL,
  INDEX IX_Job_PostedBy (PostedByUserId),
  INDEX IX_Job_Status (Status),
  CONSTRAINT FK_Job_Users FOREIGN KEY (PostedByUserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_Job_Category FOREIGN KEY (JobCategoryId) REFERENCES JobCategory (Id)
);

CREATE TABLE IF NOT EXISTS UserAttachment (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId VARCHAR(128) NOT NULL,
  AttachmentType VARCHAR(32) NOT NULL,
  StoragePath VARCHAR(512) NOT NULL,
  OriginalFilename VARCHAR(512) NULL,
  ContentType VARCHAR(255) NULL,
  SizeBytes BIGINT NOT NULL,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_UserAttachment_UserId (UserId),
  CONSTRAINT FK_UserAttachment_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AnalyticsSession (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  SessionKey VARCHAR(128) NOT NULL,
  UserId VARCHAR(128) NULL,
  LandingPath VARCHAR(512) NULL,
  Device VARCHAR(64) NULL,
  IpAddress VARCHAR(50) NULL,
  CreatedAt DATETIME NOT NULL,
  LastSeenAt DATETIME NOT NULL,
  INDEX IX_AnalyticsSession_Key (SessionKey)
);

CREATE TABLE IF NOT EXISTS AnalyticsPageEngagement (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  SessionId INT NULL,
  Path VARCHAR(512) NULL,
  DurationSeconds INT NOT NULL,
  CreatedAt DATETIME NOT NULL
);

INSERT INTO MembershipPlan (Code, Name, Description, PriceAmount, Currency, BillingPeriod, CreatedAt, UpdatedAt)
SELECT 'FREE', 'Free', 'Default plan', 0, 'INR', 'NONE', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM MembershipPlan WHERE Code = 'FREE');

INSERT INTO MembershipPlan (Code, Name, Description, PriceAmount, Currency, BillingPeriod, CreatedAt, UpdatedAt)
SELECT 'PAID_PRO', 'Premium', 'Premium membership', 99.00, 'INR', 'MONTHLY', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM MembershipPlan WHERE Code = 'PAID_PRO');

INSERT INTO JobCategory (Code, Name)
SELECT 'GENERAL', 'General'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM JobCategory WHERE Code = 'GENERAL');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'contact.email', 'yantrasearch@gmail.com', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'contact.email');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'contact.phone', '', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'contact.phone');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'contact.whatsapp', '', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'contact.whatsapp');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'upi.vpa', '', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'upi.vpa');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'upi.payeeName', 'YantraSearch', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'upi.payeeName');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'upi.payeeMobile', '', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'upi.payeeMobile');

INSERT INTO AppSetting (SettingKey, Value, CreatedAt, UpdatedAt)
SELECT 'mail.from', 'yantrasearch.help@gmail.com', NOW(), NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM AppSetting WHERE SettingKey = 'mail.from');
