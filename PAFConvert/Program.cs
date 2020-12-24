using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using ShellProgressBar;

namespace PAFConvert
{
	class Program
	{
		private static IConfiguration _configuration;
		private static ChildProgressBar _sqlBulkProgressBar;
		private static int _sqlBulkRowCount;
		private static string _sqlBulkTableName;

		static void Main(string[] args)
        {
			// Get configuration data
			string apppath = Directory.GetCurrentDirectory();
			int commitChunkCount = 10000;

			var builder = new ConfigurationBuilder()
						.SetBasePath(Directory.GetCurrentDirectory())
						.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
			_configuration = builder.Build();
			string sqliteConnString = _configuration.GetConnectionString("StagingDB");
			string mssqlConnString = _configuration.GetConnectionString("LiveDB");

			// Prepare intro
			Console.Title = "PAF Conversion Tool";
			Console.WriteLine("PAF Conversion Tool");
			Console.WriteLine("");

			// Set up Progress Bar
			var progressOptions = new ProgressBarOptions
			{
				ForegroundColor = ConsoleColor.DarkCyan,
				BackgroundColor = ConsoleColor.DarkGray,
				CollapseWhenFinished = false
			};
			var progressChildOptions = new ProgressBarOptions
			{
				ForegroundColor = ConsoleColor.White,
				BackgroundColor = ConsoleColor.DarkGray,
				ProgressCharacter = '─',
				CollapseWhenFinished = false
			};
			var progressBar = new ProgressBar(4, "Preparing", progressOptions);

			// Connect to staging SQLite DB
			SQLiteConnection sqliteConn;
			SQLiteCommand sqliteCmd;
			sqliteConn = new SQLiteConnection(sqliteConnString);
            sqliteConn.Open();
			sqliteCmd = sqliteConn.CreateCommand();

			// Prepare DB
            sqliteCmd.CommandText = @"
				CREATE TABLE ""paf"" (
					""postcode""	varchar(8) NOT NULL,
					""post_town""	varchar(30) NOT NULL,
					""dependent_locality""	varchar(35),
					""double_dependent_locality""	varchar(35),
					""thoroughfare""	varchar(80),
					""dependent_thoroughfare""	varchar(80),
					""building_number""	int,
					""building_name""	varchar(50),
					""sub_building_name""	varchar(30),
					""po_box""	varchar(6),
					""department_name""	varchar(60),
					""organisation_name""	varchar(60),
					""udprn""	int NOT NULL,
					PRIMARY KEY(""udprn"")
				);
				CREATE TABLE ""thoroughfares"" (
					""id""	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
					""thoroughfare""	varchar(80) NOT NULL
				);
				CREATE TABLE ""localities"" (
					""id""	INTEGER PRIMARY KEY AUTOINCREMENT,
					""post_town""	varchar(30),
					""dependent_locality""	varchar(35),
					""double_dependent_locality""	varchar(35)
				);
				CREATE UNIQUE INDEX IF NOT EXISTS ""idx_thoroughfares"" ON ""thoroughfares"" (
					""thoroughfare""
				);
				CREATE UNIQUE INDEX IF NOT EXISTS ""idx_localities"" ON ""localities"" (
					""post_town"",
					""dependent_locality"",
					""double_dependent_locality""
				);
			";
            sqliteCmd.ExecuteNonQuery();

            progressBar.Tick("Importing data from CSV PAF");

            string csvPath = apppath + "CSV PAF.csv";
            int rowCount = 0;

			// Check for CSV file and number of rows
			IEnumerable<PAFRecord> rows;
            try
            {
                rowCount = File.ReadLines(csvPath).Count();

                if (rowCount == 0)
                {
                    Console.WriteLine("ERROR: No data found in CSV PAF.");
                    Environment.Exit(1);
                }
            }
			#pragma warning disable CS0168 // Variable is declared but never used
            catch (FileNotFoundException e)
			#pragma warning restore CS0168 // Variable is declared but never used
            {
                rows = null;
                Console.WriteLine("ERROR: The file 'CSV PAF.csv' was not found in the application directory.");
                Environment.Exit(1);
            }

			// Read CSV and insert data into staging DB
            using (var reader = new StreamReader(csvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
				csv.Configuration.HasHeaderRecord = false;
                csv.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;

				rows = csv.GetRecords<PAFRecord>();

				var csvProgressBar = progressBar.Spawn(rowCount, "Importing " + rowCount + " address records from CSV", progressChildOptions);

                var sqliteInsertCmd = sqliteConn.CreateCommand();
                sqliteInsertCmd.CommandText = "INSERT INTO paf VALUES (@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12)";

                var sqliteCsvTransaction = sqliteConn.BeginTransaction();

                int i = 0;

				foreach (var item in rows)
                {
                    if ((i % commitChunkCount) == 0)
                    {
                        sqliteCsvTransaction.Commit();
						csvProgressBar.Tick(i, "Imported " + i.ToString() + "/" + rowCount.ToString() + " rows from CSV");
						sqliteCsvTransaction = sqliteConn.BeginTransaction();
                    }
                    sqliteInsertCmd.Parameters.Clear();
                    sqliteInsertCmd.Parameters.AddWithValue("@0", item.Postcode);
                    sqliteInsertCmd.Parameters.AddWithValue("@1", item.PostTown);
                    sqliteInsertCmd.Parameters.AddWithValue("@2", item.DependentLocality);
                    sqliteInsertCmd.Parameters.AddWithValue("@3", item.DoubleDependentLocality);
                    sqliteInsertCmd.Parameters.AddWithValue("@4", item.Thoroughfare);
                    sqliteInsertCmd.Parameters.AddWithValue("@5", item.DependentThoroughfare);
                    sqliteInsertCmd.Parameters.AddWithValue("@6", item.BuildingNumber);
                    sqliteInsertCmd.Parameters.AddWithValue("@7", item.BuildingName);
                    sqliteInsertCmd.Parameters.AddWithValue("@8", item.SubBuildingName);
                    sqliteInsertCmd.Parameters.AddWithValue("@9", item.POBox);
                    sqliteInsertCmd.Parameters.AddWithValue("@10", item.DepartmentName);
                    sqliteInsertCmd.Parameters.AddWithValue("@11", item.OrganisationName);
                    sqliteInsertCmd.Parameters.AddWithValue("@12", item.UDPRN);
                    sqliteInsertCmd.ExecuteNonQuery();
                    i++;
                }

                sqliteCsvTransaction.Commit();
				csvProgressBar.Tick(i, "Imported " + i.ToString() + "/" + rowCount.ToString() + " rows from CSV");
				sqliteCsvTransaction.Dispose();
            }

            progressBar.Tick("Converting data for use");

            var convertProgressBar = progressBar.Spawn(4, "Processing...", progressChildOptions);

			// Create localities
			convertProgressBar.Tick("Step 1 / 3: Create locality records");
            sqliteCmd.CommandText = @"
				UPDATE localities SET
					dependent_locality = IFNULL(dependent_locality, ''),
					double_dependent_locality = IFNULL(double_dependent_locality, '');
				INSERT OR IGNORE INTO localities(post_town, dependent_locality, double_dependent_locality) SELECT DISTINCT post_town, dependent_locality, double_dependent_locality FROM paf;
				UPDATE localities SET
					dependent_locality = NULLIF(dependent_locality, ''),
					double_dependent_locality = NULLIF(double_dependent_locality, '');
			";
            sqliteCmd.ExecuteNonQuery();

			// Convert blanks to NULLs
			convertProgressBar.Tick("Step 2 / 3: Convert blank values to NULLs");
			sqliteCmd.CommandText = @"UPDATE paf SET
				dependent_locality = NULLIF(dependent_locality, ''),
				double_dependent_locality = NULLIF(double_dependent_locality, ''),
				thoroughfare = NULLIF(thoroughfare, ''),
				dependent_thoroughfare = NULLIF(dependent_thoroughfare, ''),
				building_number = NULLIF(building_number, ' '),
				building_name = NULLIF(building_name, ''),
				sub_building_name = NULLIF(sub_building_name, ''),
				po_box = NULLIF(po_box, ''),
				department_name = NULLIF(department_name, ''),
				organisation_name = NULLIF(organisation_name, '');";
			sqliteCmd.ExecuteNonQuery();

			// Create thoroughfares
			convertProgressBar.Tick("Step 3 / 3: Create thoroughfare records");
            sqliteCmd.CommandText = @"
				INSERT OR IGNORE INTO thoroughfares(thoroughfare) SELECT DISTINCT thoroughfare FROM paf WHERE thoroughfare IS NOT NULL;
				INSERT OR IGNORE INTO thoroughfares(thoroughfare) SELECT DISTINCT dependent_thoroughfare FROM paf WHERE dependent_thoroughfare IS NOT NULL;
			";
            sqliteCmd.ExecuteNonQuery();

            sqliteCmd.Parameters.Clear();

			convertProgressBar.Tick("Conversion complete");

            progressBar.Tick("Uploading data to live database");
			var mssqlProgressBar = progressBar.Spawn(6, "Processing...", progressChildOptions);

			using (SqlConnection destinationConnection =
						new SqlConnection(mssqlConnString))
			{
				destinationConnection.Open();

				// Disable indexes and truncate
				mssqlProgressBar.Tick("Preparing database");
				SqlCommand preparationCommand = new SqlCommand(@"
						ALTER INDEX ALL ON paf.addresses DISABLE;
						TRUNCATE TABLE paf.thoroughfares;
						TRUNCATE TABLE paf.localities;
						TRUNCATE TABLE paf.addresses;
						ALTER INDEX PK_addresses ON paf.addresses REBUILD;
					",
					destinationConnection);
				preparationCommand.CommandTimeout = 3600;
				preparationCommand.ExecuteNonQuery();

				using (SqlBulkCopy bulkCopy =
							new SqlBulkCopy(mssqlConnString, SqlBulkCopyOptions.TableLock))
				{
					bulkCopy.NotifyAfter = commitChunkCount / 10;
					bulkCopy.BulkCopyTimeout = 300;
					bulkCopy.BatchSize = commitChunkCount;
					bulkCopy.SqlRowsCopied += new SqlRowsCopiedEventHandler(OnSqlRowsCopied);

					// Copy thoroughfares
					mssqlProgressBar.Tick("Copying thoroughfares");
					bulkCopy.DestinationTableName = _sqlBulkTableName = "paf.thoroughfares";

					sqliteCmd.CommandText = "SELECT COUNT(*) FROM thoroughfares";
					_sqlBulkRowCount = (int)(long)sqliteCmd.ExecuteScalar();
					sqliteCmd.CommandText = "SELECT * FROM thoroughfares";
					var thoroughfaresRdr = sqliteCmd.ExecuteReader();

					_sqlBulkProgressBar = mssqlProgressBar.Spawn(_sqlBulkRowCount, "Copied 0/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);

					bulkCopy.WriteToServer(thoroughfaresRdr);
					thoroughfaresRdr.Close();
					
					_sqlBulkProgressBar.Tick(_sqlBulkRowCount, "Copied " + _sqlBulkRowCount.ToString() + "/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);

					// Copy localities
					mssqlProgressBar.Tick("Copying localities");
					bulkCopy.DestinationTableName = _sqlBulkTableName = "paf.localities";

					sqliteCmd.CommandText = "SELECT COUNT(*) FROM localities";
					_sqlBulkRowCount = (int)(long)sqliteCmd.ExecuteScalar();
					sqliteCmd.CommandText = "SELECT * FROM localities";
					var localitiesRdr = sqliteCmd.ExecuteReader();

					_sqlBulkProgressBar = mssqlProgressBar.Spawn(_sqlBulkRowCount, "Copied 0/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);

					bulkCopy.WriteToServer(localitiesRdr);
					localitiesRdr.Close();

					_sqlBulkProgressBar.Tick(_sqlBulkRowCount, "Copied " + _sqlBulkRowCount.ToString() + "/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);

					// Copy addresses
					mssqlProgressBar.Tick("Copying addresses");
					bulkCopy.DestinationTableName = _sqlBulkTableName = "paf.addresses";
					bulkCopy.NotifyAfter = commitChunkCount;

					sqliteCmd.CommandText = "SELECT COUNT(*) FROM paf";
					_sqlBulkRowCount = (int)(long)sqliteCmd.ExecuteScalar();
					sqliteCmd.CommandText = @"
						SELECT
							p.udprn AS id,
							p.postcode,
							l.id AS locality_id,
							t.id AS thoroughfare_id,
							dt.id AS dependent_thoroughfare_id,
							p.building_number,
							p.building_name,
							p.sub_building_name,
							p.po_box,
							p.department_name,
							p.organisation_name,
							REPLACE(p.postcode, ' ', '') AS postcode_joined
						FROM paf p
							LEFT JOIN localities l ON(
								p.post_town IS l.post_town
								AND p.dependent_locality IS l.dependent_locality
								AND p.double_dependent_locality IS l.double_dependent_locality
							)
							LEFT JOIN thoroughfares t ON p.thoroughfare IS t.thoroughfare
							LEFT JOIN thoroughfares dt ON p.dependent_thoroughfare IS dt.thoroughfare
						ORDER BY id ASC
					";
					var addressesRdr = sqliteCmd.ExecuteReader();

					_sqlBulkProgressBar = mssqlProgressBar.Spawn(_sqlBulkRowCount, "Copied 0/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);

					bulkCopy.WriteToServer(addressesRdr);
					addressesRdr.Close();

					_sqlBulkProgressBar.Tick(_sqlBulkRowCount, "Copied " + _sqlBulkRowCount.ToString() + "/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);
				}

				// Re-enable and rebuild nonclustered indexes
				mssqlProgressBar.Tick("Reindexing database");
				SqlCommand reindexCommand = destinationConnection.CreateCommand();
				reindexCommand.CommandTimeout = 7200;
				_sqlBulkProgressBar = mssqlProgressBar.Spawn(4, "Rebuilding postcode index");
				reindexCommand.CommandText = "ALTER INDEX idx_addresses_postcode ON paf.addresses REBUILD;";
				reindexCommand.ExecuteNonQuery();
				_sqlBulkProgressBar.Tick("Rebuilding postcode_joined index");
				reindexCommand.CommandText = "ALTER INDEX idx_addresses_postcode_joined ON paf.addresses REBUILD;";
				reindexCommand.ExecuteNonQuery();
				_sqlBulkProgressBar.Tick("Rebuilding locality_thoroughfare index");
				reindexCommand.CommandText = "ALTER INDEX idx_addresses_locality_thoroughfare ON paf.addresses REBUILD;";
				reindexCommand.ExecuteNonQuery();
				_sqlBulkProgressBar.Tick("Rebuilding locality_dependent_thoroughfare index");
				reindexCommand.CommandText = "ALTER INDEX idx_addresses_locality_dependent_thoroughfare ON paf.addresses REBUILD;";
				reindexCommand.ExecuteNonQuery();
				_sqlBulkProgressBar.Tick("Reindex complete");

				mssqlProgressBar.Tick("Database upload complete");
			}

			progressBar.Tick("Complete! Press the ENTER key to exit.");

			Console.ReadLine();
		}

		private static void OnSqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
		{
			_sqlBulkProgressBar.Tick((int)e.RowsCopied, "Copied " + e.RowsCopied.ToString() + "/" + _sqlBulkRowCount.ToString() + " rows to " + _sqlBulkTableName);
		}
	}
}
