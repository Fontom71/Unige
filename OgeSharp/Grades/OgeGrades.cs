﻿using HtmlAgilityPack;
using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using UnigeWebUtility;

namespace OgeSharp {

    public partial class Oge {

        /// <summary>
        /// Regex that let's me extract numbers from a string<br/>
        /// Example: "QCM [17.00 /20.0(1.0) 20.00 /20.0(1.0) 17.50 /20.0(1.0) ](1.0)"<br/>
        /// Will return all the numbers: 17.00, 20.0, 1.0, 20.00, 20.0, 1.0, 17.50, 20.0, 1.0, 1.0
        /// </summary>
        internal Regex GradesRegex = new Regex(@"([0-9]+.[0-9]+)");

        /// <summary>
        /// Send a request to get the row's childs and returns them<br/>
        /// Returns null if the row do not have any child (mean it's not a folder)
        /// </summary>
        private HtmlNodeCollection GetRowChilds(HtmlNode row) {

            // TODO: Optimization send multiple requests at the same time
            // Requests are the bottleneck here

            // Check if the row is a folder
            // => It's a folder only if the latest span do not have a style attribute
            if (string.IsNullOrEmpty(row.SelectSingleNode("./td[1]/span[last()]").GetAttributeValue("style", null))) {

                // Optimization: I should not always send a request everytime
                // => By default the first semester is opened
                // => But for simplicity let's just always send a request

                // Get the row id (needed for the ajax request)
                string rowID = row.GetAttributeValue("data-rk", null);

                // Initialize a request with POST method
                HttpWebRequest request = Browser.CreateRequest(GradesUri);
                request.Method = "POST";

                // Set the request content
                request.SetContent("application/x-www-form-urlencoded",
                    $"javax.faces.partial.ajax=true&javax.faces.partial.render=mainBilanForm%3AtreeTable&mainBilanForm%3AtreeTable_expand={rowID}");

                // Process the request and extract the row's html
                HttpWebResponse response = Browser.ProcessRequest(request);
                string html = response.GetContent().Split("[CDATA[")[1].Split("]]")[0];

                // Parse the html as a document
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);

                // Return the row's node
                return document.DocumentNode.SelectNodes("./tr");

            }
            return null;

        }

        /// <summary>
        /// Processes a row and it's childs recursively
        /// </summary>
        private void ProcessRow(GradeEntry entry, HtmlNode row) {

            // Get the coefficient text
            string coefficientText = row.SelectSingleNode(".//td[2]").InnerText;

            // If the row do not have a coefficient then we just drop it
            // => Resolves crash when there is no coefficient
            if (string.IsNullOrEmpty(coefficientText)) return;

            // Get the row name and coefficient
            string name = row.SelectSingleNode("./td[1]").GetDirectInnerText();
            double coefficient = double.Parse(coefficientText, CultureInfo.InvariantCulture);

            #region Folder processing

            // Try to get the row's childs
            HtmlNodeCollection childs = GetRowChilds(row);

            // If it's a folder row
            if (childs != null) {

                // Create the folder's entry and add it to the parent
                GradeEntry folderEntry = new GradeEntry(name, coefficient);
                entry.Entries.Add(folderEntry);

                // Loop through the childs and process them
                foreach (HtmlNode child in childs) ProcessRow(folderEntry, child);

            }

            #endregion
            #region Subject processing

            else {

                // Initialize a row entry and add to the parent
                GradeEntry rowEntry = new GradeEntry(name, coefficient);
                entry.Entries.Add(rowEntry);

                // Get the grades text (third column)
                string gradesText = row.SelectSingleNode("./td[3]").InnerText;

                // If the subject have some grades
                if (!string.IsNullOrEmpty(gradesText)) {

                    // Loop through each lines of grades (looks like this):
                    // => gradeName [10.00 /10.0(1.0)   ](1.0)
                    // => gradeName  [4.50 /5.0(1.0)  8.00 /10.0(1.0)   ](1.0)
                    foreach (string line in gradesText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {

                        // Get the grade name
                        string gradeName = line.Split('[')[0].TrimEnd();

                        // Get the grades and the coefficients
                        MatchCollection matches = GradesRegex.Matches(line);

                        // Initialize the grade entry
                        GradeEntry gradeEntry = new GradeEntry(gradeName, double.Parse(matches[^1].Groups[1].Value, CultureInfo.InvariantCulture));
                        rowEntry.Entries.Add(gradeEntry);

                        // Loop through the grades
                        for (int n = 0; n < matches.Count - 1; n += 3) {

                            // Get the grade, max grade and coefficient
                            double grade = double.Parse(matches[n].Groups[1].Value, CultureInfo.InvariantCulture);
                            double maxGrade = double.Parse(matches[n + 1].Groups[1].Value, CultureInfo.InvariantCulture);
                            double gradeCoefficient = double.Parse(matches[n + 2].Groups[1].Value, CultureInfo.InvariantCulture);

                            // Add a new grade entry
                            gradeEntry.Entries.Add(new GradeEntry(grade, maxGrade, gradeCoefficient));

                        }

                    }

                }

            }

            #endregion

        }

        /// <summary>
        /// Returns an entry containing all your grades
        /// </summary>
        public GradeEntry GetGrades() {

            // Download the grades page source code and parse it as a document
            string gradesSource = Browser.Navigate(GradesUri).GetContent();
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(gradesSource);

            // Create a root entry
            GradeEntry rootEntry = new GradeEntry("Root", 1);

            // Loop through all the rows that have a UI Level of 1
            // => It's the top most rows
            foreach (HtmlNode topRow in document.DocumentNode.SelectNodes("//tbody/tr[contains(@class, 'ui-node-level-1')]")) {

                // Process the row
                ProcessRow(rootEntry, topRow);

            }
            return rootEntry;

        }

    }

}
