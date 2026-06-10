// Ensures Visual Studio setup projects create regular shortcuts instead of
// advertised shortcuts. SendTo menu entries do not work reliably when the MSI
// creates advertised shortcuts, so the generated MSI must set this property.
if (WScript.Arguments.length === 0) {
	WScript.Echo("Usage: cscript.exe //nologo DisableAdvertisedShortcuts.js <msi path>");
	WScript.Quit(1);
}

var msiPath = WScript.Arguments(0);
var installer = WScript.CreateObject("WindowsInstaller.Installer");
var database = installer.OpenDatabase(msiPath, 1);

function executeSql(sql) {
	var view = database.OpenView(sql);
	view.Execute();
	view.Close();
}

var selectView = database.OpenView("SELECT `Value` FROM `Property` WHERE `Property`='DISABLEADVTSHORTCUTS'");
selectView.Execute();
var record = selectView.Fetch();
selectView.Close();

if (record) {
	executeSql("UPDATE `Property` SET `Value`='1' WHERE `Property`='DISABLEADVTSHORTCUTS'");
} else {
	executeSql("INSERT INTO `Property` (`Property`, `Value`) VALUES ('DISABLEADVTSHORTCUTS', '1')");
}

database.Commit();
WScript.Echo("Set DISABLEADVTSHORTCUTS=1 in " + msiPath);
