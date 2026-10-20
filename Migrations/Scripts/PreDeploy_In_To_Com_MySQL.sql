-- Everything the new API (.in) needs on top of the schema the current .com site uses
-- (yantrasearch-backend-dotnet, branch simplified_login, commit e792a1a).
--
-- No DROP and no change to existing rows. The only change to existing columns is section 1b, which
-- widens two VendorProfile document-path columns (never shortens, no data change).
-- Safe to run more than once. Run it on the database selected with USE <db>; it acts on DATABASE().
-- The current .com site keeps working after this runs: it sets no EF initializer and only reads the
-- columns it maps, every new column is NULL or has a default, and the new tables are not used by it.
--
-- Contents
--   1. New columns on existing tables (10)
--   1b. Wider VendorProfile.GSTIN_File / PAN_File (15 / 10 -> 255) for stored document paths
--   2. New tables (14)
--   3. Default rows (membership plans, job category, site settings) - inserted only when missing
--   4. Check query

SET @db = DATABASE();

-- ---------------------------------------------------------------------------
-- 1. New columns on existing tables
-- ---------------------------------------------------------------------------

-- ContactMessage.Phone (contact form phone)
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ContactMessage' AND COLUMN_NAME = 'Phone'),
  'SELECT 1', 'ALTER TABLE ContactMessage ADD COLUMN Phone VARCHAR(50) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Equipment.UpdatedByName (who last edited the item)
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Equipment' AND COLUMN_NAME = 'UpdatedByName'),
  'SELECT 1', 'ALTER TABLE Equipment ADD COLUMN UpdatedByName VARCHAR(150) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- VendorProfile.SupplierKind (equipment / materials supplier)
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'VendorProfile' AND COLUMN_NAME = 'SupplierKind'),
  'SELECT 1', 'ALTER TABLE VendorProfile ADD COLUMN SupplierKind VARCHAR(32) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- EmployeeProfile.CurrentCtcText
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'EmployeeProfile' AND COLUMN_NAME = 'CurrentCtcText'),
  'SELECT 1', 'ALTER TABLE EmployeeProfile ADD COLUMN CurrentCtcText VARCHAR(100) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- EmployeeProfile.ExpectedCtcText
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'EmployeeProfile' AND COLUMN_NAME = 'ExpectedCtcText'),
  'SELECT 1', 'ALTER TABLE EmployeeProfile ADD COLUMN ExpectedCtcText VARCHAR(100) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- EmployeeProfile.AddressLine1
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'EmployeeProfile' AND COLUMN_NAME = 'AddressLine1'),
  'SELECT 1', 'ALTER TABLE EmployeeProfile ADD COLUMN AddressLine1 VARCHAR(255) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- EmployeeProfile.PinCode
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'EmployeeProfile' AND COLUMN_NAME = 'PinCode'),
  'SELECT 1', 'ALTER TABLE EmployeeProfile ADD COLUMN PinCode VARCHAR(10) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ContractorProfile.HasNoPriorProjects (existing rows get 0 = has projects / not stated)
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ContractorProfile' AND COLUMN_NAME = 'HasNoPriorProjects'),
  'SELECT 1', 'ALTER TABLE ContractorProfile ADD COLUMN HasNoPriorProjects TINYINT(1) NOT NULL DEFAULT 0'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ContractorProject.ClientName
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ContractorProject' AND COLUMN_NAME = 'ClientName'),
  'SELECT 1', 'ALTER TABLE ContractorProject ADD COLUMN ClientName VARCHAR(200) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ContractorProject.LabourStrength
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ContractorProject' AND COLUMN_NAME = 'LabourStrength'),
  'SELECT 1', 'ALTER TABLE ContractorProject ADD COLUMN LabourStrength VARCHAR(100) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 1b. VendorProfile document paths. The .com migration made GSTIN_File VARCHAR(15) and PAN_File
--     VARCHAR(10), too short for a path like /Uploads/VendorDocs/GST_<guid>.pdf. Widened to 255,
--     the same as ContractorProfile.GSTIN_File / PAN_File. Skipped when already 255 or wider.
-- ---------------------------------------------------------------------------

SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'VendorProfile' AND COLUMN_NAME = 'GSTIN_File'
    AND CHARACTER_MAXIMUM_LENGTH < 255),
  'ALTER TABLE VendorProfile MODIFY COLUMN GSTIN_File VARCHAR(255) NULL', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'VendorProfile' AND COLUMN_NAME = 'PAN_File'
    AND CHARACTER_MAXIMUM_LENGTH < 255),
  'ALTER TABLE VendorProfile MODIFY COLUMN PAN_File VARCHAR(255) NULL', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 2. New tables (parents before children)
-- ---------------------------------------------------------------------------

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
  ImageUrl VARCHAR(512) NULL,
  UpdatedByName VARCHAR(150) NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  DeletedAt DATETIME NULL,
  INDEX IX_SupplierOffering_Vendor (VendorProfileId),
  CONSTRAINT FK_SupplierOffering_Vendor FOREIGN KEY (VendorProfileId) REFERENCES VendorProfile (Id) ON DELETE CASCADE
);

-- Covers a SupplierOffering table created earlier by the shorter script without these two columns.
SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'SupplierOffering' AND COLUMN_NAME = 'ImageUrl'),
  'SELECT 1', 'ALTER TABLE SupplierOffering ADD COLUMN ImageUrl VARCHAR(512) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = (SELECT IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'SupplierOffering' AND COLUMN_NAME = 'UpdatedByName'),
  'SELECT 1', 'ALTER TABLE SupplierOffering ADD COLUMN UpdatedByName VARCHAR(150) NULL'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

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

CREATE TABLE IF NOT EXISTS SavedJobSeeker (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ClientId VARCHAR(128) NOT NULL,
  EmployeeId INT NOT NULL,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_SavedJobSeeker_Client (ClientId),
  CONSTRAINT FK_SavedJobSeeker_Users FOREIGN KEY (ClientId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_SavedJobSeeker_Employee FOREIGN KEY (EmployeeId) REFERENCES EmployeeProfile (EmployeeId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ListingActivity (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ClientUserId VARCHAR(128) NOT NULL,
  ListingType VARCHAR(32) NOT NULL,
  ListingId VARCHAR(64) NOT NULL,
  ListingTitle VARCHAR(255) NULL,
  OwnerUserId VARCHAR(128) NULL,
  Kind VARCHAR(32) NOT NULL,
  Note VARCHAR(2000) NULL,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_ListingActivity_Owner (OwnerUserId, CreatedAt),
  INDEX IX_ListingActivity_Client (ClientUserId, ListingType, ListingId)
);

-- ---------------------------------------------------------------------------
-- 3. Default rows - inserted only when the key is missing, never overwritten.
--    Values you already set on .in (UPI VPA, contact phone, plan price) must be
--    re-entered on .com from Admin after deploy, or copied from the .in AppSetting table.
-- ---------------------------------------------------------------------------

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

-- ---------------------------------------------------------------------------
-- 4. Check: expect 14 tables and 12 columns (10 above + SupplierOffering ImageUrl/UpdatedByName).
-- ---------------------------------------------------------------------------

SELECT COUNT(*) AS new_tables_present FROM information_schema.TABLES
WHERE TABLE_SCHEMA = @db AND TABLE_NAME IN (
  'ClientRequirement','ClientRequirementAttachment','MembershipPlan','UserMembership','PaymentOrder',
  'AppSetting','SupplierOffering','JobCategory','Job','UserAttachment','AnalyticsSession',
  'AnalyticsPageEngagement','SavedJobSeeker','ListingActivity');

SELECT COUNT(*) AS new_columns_present FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND (
     (TABLE_NAME = 'ContactMessage'    AND COLUMN_NAME = 'Phone')
  OR (TABLE_NAME = 'Equipment'         AND COLUMN_NAME = 'UpdatedByName')
  OR (TABLE_NAME = 'VendorProfile'     AND COLUMN_NAME = 'SupplierKind')
  OR (TABLE_NAME = 'EmployeeProfile'   AND COLUMN_NAME IN ('CurrentCtcText','ExpectedCtcText','AddressLine1','PinCode'))
  OR (TABLE_NAME = 'ContractorProfile' AND COLUMN_NAME = 'HasNoPriorProjects')
  OR (TABLE_NAME = 'ContractorProject' AND COLUMN_NAME IN ('ClientName','LabourStrength'))
  OR (TABLE_NAME = 'SupplierOffering'  AND COLUMN_NAME IN ('ImageUrl','UpdatedByName')));

-- Expect 2 rows, both 255.
SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'VendorProfile' AND COLUMN_NAME IN ('GSTIN_File','PAN_File');
