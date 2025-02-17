﻿using System;
using System.Collections.Generic;
using System.Net;
using UnigeWebUtility;
using Utf8Json;

namespace OgeSharp {

    public partial class Oge {

        /// <summary>
        /// Returns the schedule of the current day
        /// </summary>
        public Schedule GetSchedule() => GetSchedule(DateTime.Now);

        /// <summary>
        /// Returns the schedule of the specified day<br/>
        /// (Note: OGE ignores the time part of the datetime. So it will return all the time slots of the specified day)
        /// </summary>
        public Schedule GetSchedule(DateTime date) => GetSchedule(date, date);

        /// <summary>
        /// Returns the schedule between the two specified days<br/>
        /// (Note: OGE ignores the time part of the datetime. So it will return all the time slots between the specified days)
        /// </summary>
        public Schedule GetSchedule(DateTime start, DateTime end) {

            // Navigate to OGE
            Browser.Navigate(ScheduleUri);

            // Initialize a request
            HttpWebRequest request = Browser.CreateRequest(ScheduleUri);

            // Set the method
            request.Method = "POST";

            // Format the dates to javascript's one
            string startDate = start.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string endDate = end.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Set the content
            // => The remaining argument are the only one that are useful
            request.SetContent("application/x-www-form-urlencoded",
                $"javax.faces.partial.ajax=true&javax.faces.partial.render=mainFormPlanning%3Aedt&mainFormPlanning%3Aedt=mainFormPlanning%3Aedt&mainFormPlanning%3Aedt_start={WebUtility.UrlEncode(startDate)}&mainFormPlanning%3Aedt_end={WebUtility.UrlEncode(endDate)}");

            // Process the request
            HttpWebResponse response = Browser.ProcessRequest(request);

            // Get the response content and extract the json data from it
            string content = response.GetContent();
            string json = content.Split("![CDATA[")[1].Split("]]")[0];

            // Conver the json string to a dictionnaries list
            List<dynamic> slots = JsonSerializer.Deserialize<dynamic>(json)["events"];

            // Initialize a schedule instance
            Schedule schedule = new(start, end, slots.Count);

            // Loop through all the time slots
            for (int i = 0; i < slots.Count; i++) {

                // Create the time slot instance and save it into the schedule
                schedule.Slots[i] = new TimeSlot(slots[i]);

            }
            return schedule;

        }

        /// <summary>
        /// Returns the schedule of the week
        /// </summary>
        public Schedule GetScheduleOfTheWeek() => GetScheduleOfTheWeek(DateTime.Now);

        /// <summary>
        /// Returns the schedule of the week of the specified day
        /// </summary>
        public Schedule GetScheduleOfTheWeek(DateTime dayOfTheWeek) {

            // Get the number of days we are away from the start of the week
            int difference = (dayOfTheWeek.DayOfWeek - DayOfWeek.Monday) % 7;
            // Substract the days to get back to monday
            DateTime startOfTheWeek = dayOfTheWeek.AddDays(-difference);

            // Add 6 days to get to the end of the week
            DateTime endOfTheWeek = startOfTheWeek.AddDays(6);

            return GetSchedule(startOfTheWeek, endOfTheWeek);

        }

    }

}
