-- Additive columns for supplier item photos and edit history.
-- Safe to run more than once.

SET @db = DATABASE();

SET @sql = (
  SELECT IF(
    EXISTS(
      SELECT 1 FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'SupplierOffering' AND COLUMN_NAME = 'ImageUrl'
    ),
    'SELECT 1',
    'ALTER TABLE SupplierOffering ADD COLUMN ImageUrl VARCHAR(512) NULL'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = (
  SELECT IF(
    EXISTS(
      SELECT 1 FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'SupplierOffering' AND COLUMN_NAME = 'UpdatedByName'
    ),
    'SELECT 1',
    'ALTER TABLE SupplierOffering ADD COLUMN UpdatedByName VARCHAR(150) NULL'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = (
  SELECT IF(
    EXISTS(
      SELECT 1 FROM information_schema.COLUMNS
      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Equipment' AND COLUMN_NAME = 'UpdatedByName'
    ),
    'SELECT 1',
    'ALTER TABLE Equipment ADD COLUMN UpdatedByName VARCHAR(150) NULL'
  )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
