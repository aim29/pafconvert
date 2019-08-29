# PAF Convert

PAF Convert is a tool that converts the [CSV Postcode Address File (PAF)](https://www.poweredbypaf.com/csv-paf/) available from [Royal Mail's Address Management Unit](https://www.poweredbypaf.com/product/paf/) into a fairly flat SQL database.

It creates an addresses table containing the majority of the data from the PAF, with lookups to a localities and thoroughfares table.

A suggested schema for a Microsoft SQL Server database to which this data could be imported is provided, including relevant indexes, views and stored procedures for address and postcode search. A more rudimentary schema is also provided for SQLite, which is the same schema used by the tool itself.

[SQLite](https://sqlite.org) is used to do the conversion locally, and a [GNU-compatible version of sed](https://www.gnu.org/software/sed/) to do some rudimentary reformatting of the SQL output (removing double quotes from table names to ensure compatibility for import into other RDBMSes like Microsoft SQL Server).

---

## Using the tool

You will want to modify the pafconvert script before running it to suit your purposes, in particular:

* Modifying output table names on lines 129-130, 134-135 and 139-140
* The sed command on line 146 may need modifying/removal

The tool expects to find the `CSV PAF.csv` file in the same folder as it is being run from. It will also create or use a database called `paf.db3` in that same folder, and will write to (or overwrite) an output file called `PAF.sql`.

To run the tool, ensure that you have SQLite and a GNU-compatible sed installed and run:

`sqlite3 paf.db3 ".read pafconvert"`

---

## Run times and database sizes

Based on testing done with the July 2019 PAF dataset, the tool takes around 10 minutes to run from scratch (on a fairly modern computer with 8GB RAM) and creates a database file of around 6.5GB. Note that the SQL database created by the tool will be very large as it creates and drops a table when processing the CSV; should you wish to keep it around for later use or for incremental changes I would suggest running a `VACUUM` on it, which should reduce the database size by around half.

---

## Where to get required software

* Windows
	* SQLite: [https://www.sqlite.org/download.html](https://www.sqlite.org/download.html) - download the sqlite-tools-win32-*.zip file, and use the sqlite3.exe executable.
	* Sed: [http://gnuwin32.sourceforge.net/packages/sed.htm](http://gnuwin32.sourceforge.net/packages/sed.htm)
	
* macOS
	* SQLite: the built-in version of SQLite 3 (`sqlite3`) will do
	* Sed: the built-in version of sed is not compatible with this script. Use [Homebrew](https://brew.sh) to install GNU sed (`brew install gnu-sed`) and modify line 146 of the script to replace `sed` with `gsed`.
	
* Linux
	* The tool was tested on Ubuntu 18.04 with the built-in version of sed and the latest sqlite3 installed using apt.

---

## Licensing

The tool is provided with no warranty of fitness for purpose and is released under the MIT Licence.
