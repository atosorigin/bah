// ReSharper disable CheckNamespace
namespace ePharmacy.PCR.Test.Web
// ReSharper restore CheckNamespace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Web.Security;
    using Common;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Win32;
    using WatiN.Core;
    using WatiN.Core.Constraints;

    /// <summary>
    /// A Web Test Base Class
    /// </summary>
    public class WebTest
    {
        protected TestUser AlternativeTestUser;
        protected internal TestUser AssociatedUser;
        protected internal IE TestBrowser;

        [TestInitialize]
        public virtual void TestInitializer()
        {
            CloseExistingInternetExplorers();
            TestBrowser = new IE();
            PerformActionWhilstSwallowingExceptions(ClearCacheAndCookies);
            AssociatedUser = TestUserHelper.GetAssociatedUser();

            // Everything else is tidy up
            PerformActionWhilstSwallowingExceptions(TestHelper.RestoreDatabase);
            PerformActionWhilstSwallowingExceptions(ConfigureWatinSettings);
            PerformActionWhilstSwallowingExceptions(EnableJavascript);
        }


        [TestCleanup]
        public void TestCleanup()
        {
            SignOutUser();
            PerformActionWhilstSwallowingExceptions(TestBrowser.Close);
            PerformActionWhilstSwallowingExceptions(AssociatedUser.Delete);
            PerformActionWhilstSwallowingExceptions(() => { if (AlternativeTestUser != null) AlternativeTestUser.Delete(); });
            PerformActionWhilstSwallowingExceptions(EnableJavascript);
        }

        private void SignOutUser()
        {
            PerformActionWhilstSwallowingExceptions(() =>

                                                        {
                                                            if (!AssociatedUser.UserIsDeleted)
                                                            {
                                                                TestBrowser.PCRSignOut();
                                                            }
                                                        });
        }

        private void ClearCacheAndCookies()
        {
            TestBrowser.ClearCache();
            TestBrowser.ClearCookies();   
        }

        private static void CloseExistingInternetExplorers()
        {
            PerformActionWhilstSwallowingExceptions(() =>
                                                        {
                                                            foreach (var internetExplorer in IE.InternetExplorers())
                                                            {
                                                                internetExplorer.ForceClose();
                                                            }
                                                        });
        }

        private static void PerformActionWhilstSwallowingExceptions(Action action)
        {
            try
            {
                action.Invoke();
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
            }
        }


        private static void ConfigureWatinSettings()
        {
            Settings.Instance.AutoMoveMousePointerToTopLeft = false;
            Settings.SleepTime = 30;
            Settings.WaitForCompleteTimeOut = 120;
        }

        [Obsolete("Use AssertCurrentPageIsAsExpected which wraps this functionality")]
        protected Boolean CurrentPageIsAsExpected(String expectedPage)
        {
            return CheckCurrentPage(expectedPage);
        }

        private bool CheckCurrentPage(string expectedPage)
        {
            string pageName = TestBrowser.Uri.Segments.Last();
            return string.Equals(expectedPage, pageName, StringComparison.InvariantCultureIgnoreCase);
        }

        protected void AssertCurrentPageIsAsExpected(string expectedPage)
        {
            bool currentPageIsAsExpected = CheckCurrentPage(expectedPage);
            Assert.IsTrue(currentPageIsAsExpected,
                          "Expected: " + expectedPage + " but found: " + TestBrowser.Uri.Segments.Last());
        }

        protected void ApplicationNotOnErrorPage()
        {
            Span span = TryGetSpan("lblError");
            Assert.IsFalse(span.Exists);
        }

        protected void ApplicationHasErrored()
        {
            GetSpan("lblError");
        }

        protected void Login()
        {
            Login(AssociatedUser.UserName, TestDataConstants.UserPassword);
        }

        protected void Login(String userName, String password)
        {
            if (CheckCurrentPage("blank")) TestBrowser.GoTo(UrlAndControlConstants.LogonUrl);
            AssertCurrentPageIsAsExpected(UrlAndControlConstants.LogonPage);
            TestBrowser.PCRLogin(userName, password);
        }

        protected Link GetLink(string linkId)
        {
            Link link = GetElementWithTimeout<Link>(Find.ById(linkId));
            Assert.IsTrue(link.Exists, "Can't find Link with ID: " + linkId);
            return link;
        }

        protected void LinkExistsAndClick(String linkId, String expectedDestination)
        {
            Link link = GetLink(linkId);
            link.Click();
            AssertCurrentPageIsAsExpected(expectedDestination);
        }

        protected Link GetLinkByText(string linkText)
        {
            Link link = GetElementWithTimeout<Link>(Find.ByText(linkText));
            Assert.IsTrue(link.Exists, "Can't find link with inner text: " + linkText);
            return link;
        }

        /// <summary>
        /// For testing links which open in a new browser window
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="newDocumentTitle"></param>
        /// <returns></returns>
        /// <history>
        ///     Ciaran  Added Timeout to see if IE attaching can be made more repeatable
        /// </history>
        protected IE LinkExistsAndClickWithNewIE(String linkId, String newDocumentTitle)
        {
            const String PatientReportTitle = "PCR Patient Report";

            GetElementWithTimeout<Link>(Find.ById(linkId)).Click();

            IE ie = null;

            try
            {
                ie = FindIEByTitle(PatientReportTitle);
            }
            catch (Exception ieNotFoundException)
            {
                Assert.Fail("Could not attach to instance of IE with Title " + PatientReportTitle + " : " +
                            ieNotFoundException.Message);
            }

            return ie;
        }

        /// <summary>
        /// Had to write this as AttachToIE didn't work as expected on the build server
        /// </summary>
        /// <param name="pageTitle">The page title.</param>
        /// <returns></returns>
        private static IE FindIEByTitle(String pageTitle)
        {
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(1000);

                if (IE.InternetExplorers().Any())
                {
                    foreach (IE internetExplorer in IE.InternetExplorers())
                    {
                        if (pageTitle.StartsWith(internetExplorer.Title, StringComparison.CurrentCultureIgnoreCase))
                            return internetExplorer;
                    }
                }
            }

            throw new Exception("PageTitle: " + pageTitle);
        }

        protected void TextFieldsArePopulated(String[] textFieldIds)
        {
            foreach (String textFieldId in textFieldIds)
            {
                TextField textField = GetTextField(textFieldId);
                Assert.IsTrue(textField.Value.Length > 0);
            }
        }

        protected void TextFieldsArePopulated(String[] textFieldIds, Dictionary<String, String> fieldValueMap)
        {
            foreach (String textFieldId in textFieldIds)
            {
                TextField textField = GetTextField(textFieldId);
                Assert.IsTrue(textField.Value.Length > 0);
                fieldValueMap.Add(textFieldId, textField.Value);
            }
        }

        protected void CheckBoxValueCorrect(String checkBoxId, Boolean expectedValue)
        {
            CheckBox checkBox = GetCheckBox(checkBoxId);
            Assert.AreEqual(expectedValue, checkBox.Checked);
        }

        protected CheckBox GetCheckBox(string checkBoxId)
        {
            CheckBox checkBox = GetElementWithTimeout<CheckBox>(Find.ById(checkBoxId));
            Assert.IsTrue(checkBox.Exists, "Checkbox " + checkBoxId + " not found");
            return checkBox;
        }

        protected void SetCheckBoxValue(String checkBoxId, Boolean checkBoxChecked)
        {
            CheckBox checkBox = GetCheckBox(checkBoxId);
            checkBox.Checked = checkBoxChecked;
        }

        protected void SelectDropDownHasCorrectValue(String selectId, Int32 expectedValue, String expectedDisplayValue)
        {
            SelectList selectList = GetSelectList(selectId);
            Assert.IsTrue(selectList.Exists);
            Assert.AreEqual(expectedValue, Int32.Parse(selectList.SelectedOption.Value));
            Assert.AreEqual(expectedDisplayValue, selectList.SelectedItem);
        }

        protected void SelectDropDownHasCorrectValue(String selectId, String expectedDisplayValue)
        {
            SelectList selectList = GetSelectList(selectId);
            Assert.IsTrue(selectList.Exists);
            Assert.AreEqual(expectedDisplayValue, selectList.SelectedItem);
        }

        protected TextField GetTextField(String textFieldId)
        {
            TextField textField = GetElementWithTimeout<TextField>(Find.ById(textFieldId));
            Assert.IsTrue(textField.Exists, "TextField " + textFieldId + " not found.");
            return textField;
        }

        protected TableRow GetTableRow(string tableRowId)
        {
            var tableRow = GetElementWithTimeout<TableRow>(Find.ById(tableRowId));
            Assert.IsTrue(tableRow.Exists, string.Format("Could not find table row with id: {0}", tableRowId));
            return tableRow;
        }

        private T GetElementWithTimeout<T>(Constraint findBy) where T : Element
        {
            var soughtElement = TestBrowser.ElementOfType<T>(findBy);
            try
            {
                soughtElement.WaitUntilExists();
            }
            catch (WatiN.Core.Exceptions.TimeoutException timeoutException)
            {
                Trace.WriteLine("Element could not be found matching Constraint: " + findBy);
                Trace.WriteLine(timeoutException.Message);
            }
            return soughtElement;
        }

        protected Span GetSpan(String spanId)
        {
            Span span = GetElementWithTimeout<Span>(Find.ById(spanId));
            Assert.IsTrue(span.Exists);
            return span;
        }

        protected Span TryGetSpan(string spanId)
        {
            return TestBrowser.Span(Find.ById(spanId));
        }

        protected Div GetDiv(String divId)
        {
            Div div = GetElementWithTimeout<Div>(Find.ById(divId));
            Assert.IsTrue(div.Exists);
            return div;
        }

        protected T ConfirmElementMatchingCriteria<T>(Predicate<T> findElementPredicate, Predicate<T> matchingCriteriaPredicate) where T : Element
        {
            T element = TestBrowser.ElementOfType(findElementPredicate);
            try
            {
                element.WaitUntilExists();

                Assert.IsTrue(matchingCriteriaPredicate(element), "Could not find element with matching criteria");
            }
            catch (Exception)
            {
                Assert.Fail("Could not get Element");
            }
            return element;
        }

        protected Label GetLabel(String labelId)
        {
            Label label = GetElementWithTimeout<Label>(Find.ById(labelId));
            Assert.IsTrue(label.Exists);
            return label;
        }

        protected SelectList GetSelectList(String selectListId)
        {
            SelectList selectList = GetElementWithTimeout<SelectList>(Find.ById(selectListId));
            Assert.IsTrue(selectList.Exists);
            return selectList;
        }

        protected void SetValueOnSelectList(string selectListId, string setValue)
        {
            SelectList selectList = GetSelectList(selectListId);
            selectList.Select(setValue);
        }

        protected Table GetTable(String tableId)
        {
            Table table = GetElementWithTimeout<Table>(Find.ById(tableId));
            Assert.IsTrue(table.Exists, "Could not find table with id: " + tableId);
            return table;
        }

        protected Button GetButton(string buttonId)
        {
            Button foundButton = GetElementWithTimeout<Button>(Find.ById(buttonId));
            Assert.IsTrue(foundButton.Exists);
            return foundButton;
        }

        protected void ButtonExistsAndClick(String buttonId, String expectedDestination)
        {
            GetButton(buttonId).Click();
            AssertCurrentPageIsAsExpected(expectedDestination);
        }

        protected void DamageTimestamp(String timestampId)
        {
            TextField hiddenField = GetTextField(timestampId);
            Byte[] rowVersion = Convert.FromBase64String(hiddenField.Value);
            rowVersion[0] = 127;
            hiddenField.Value = Convert.ToBase64String(rowVersion);
        }

        protected string GetTimestamp(string timestampId)
        {
            TextField timestampTextField = GetTextField(timestampId);
            Assert.IsTrue(timestampTextField.Exists);
            return timestampTextField.Value;
        }

        protected void ValidateTimestamp(String timestampId)
        {
            TextField hiddenField = GetTextField(timestampId);
            Byte[] rowVersion = Convert.FromBase64String(hiddenField.Value);
            Assert.AreEqual(8, rowVersion.Length);
        }

        protected void ChangeCHINumber(String txtChi)
        {
            TextField hiddenField = GetTextField(txtChi);
            hiddenField.Value = "9008075679";
        }

        protected void PageContains(String textToCheck)
        {
            Assert.IsTrue(TestBrowser.Text.IndexOf(textToCheck, StringComparison.InvariantCultureIgnoreCase) >= 0,
                          "Text does not contain: " + textToCheck);
        }

        private const string ReportsLink = "reportsLink";
        protected void AssertGoToReportsPage()
        {
            GetLink(ReportsLink).Click();
            AssertCurrentPageIsAsExpected(UrlAndControlConstants.NMISDashboardPage);
        }

        protected String GeneratePassword()
        {
            return Membership.GeneratePassword(UrlAndControlConstants.PasswordLength,
                                               UrlAndControlConstants.NonAlphanumericCharacters);
        }

        /// <summary>
        /// Checks the focus has been set correctly.
        /// </summary>
        /// <history>
        /// 2010-02-18  Tim  Created - TTP 503
        /// </history>
        protected void AssertFocusSetCorrectly(string elementId)
        {
            Assert.IsTrue(TestBrowser.ActiveElement.Id == elementId);
        }

        protected void QueryStringContainsKeyValue(String key)
        {
            string queryString = TestBrowser.Uri.Query;
            Assert.IsTrue(queryString.Contains(key), key + " not found in queryString " + queryString);
        }

        protected RadioButton GetRadioButton(string radioButtonId)
        {
            RadioButton radioButton = GetElementWithTimeout<RadioButton>(Find.ById(radioButtonId));
            Assert.IsTrue(radioButton.Exists);
            return radioButton;
        }

        protected void SetRadioButtonValue(string radioButtonId, bool radioButtonChecked)
        {
            RadioButton radioButton = GetRadioButton(radioButtonId);
            radioButton.Checked = radioButtonChecked;
        }

        protected void SetTextField(string textFiedlId, string textFieldValue)
        {
            TextField textField = GetTextField(textFiedlId);
            textField.Value = textFieldValue;
        }

        protected void AssertGo(string url)
        {
            TestBrowser.GoTo(url);
            var targetUri = new Uri(url);
            AssertCurrentPageIsAsExpected(targetUri.Segments.Last());
        }

        private const int JavascriptOn = 0;
        private const int JavascriptOff = 3;
        private const string JavascriptEnableDisableRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\Zones\\2";
        private const string RegistryKeyValue = "1400";

        protected void DisableJavascript()
        {
            SetJavascriptOffOrOn(JavascriptOff);
        }

        protected void EnableJavascript()
        {
            SetJavascriptOffOrOn(JavascriptOn);
        }

        private static void SetJavascriptOffOrOn(int registryValue)
        {
            // Check the current value and only progress to changing it if the value is different.
            using (var registryKey = Registry.CurrentUser.OpenSubKey(JavascriptEnableDisableRegistryKey))
            {
                if (registryKey != null)
                {
                    var currentValue = (int)registryKey.GetValue(RegistryKeyValue);
                    if (currentValue == registryValue) return;
                }
            }

            using (var regkey = Registry.CurrentUser.OpenSubKey(JavascriptEnableDisableRegistryKey, true))
            {
                if (regkey != null)
                {
                    regkey.SetValue(RegistryKeyValue, registryValue);
                }
            }
        }
    }
}
