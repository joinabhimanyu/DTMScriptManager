using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DTMScriptManager.Models
{
    public class wsdl_report_file_path_info
    {
        public string num_line_of_business_code { get; set; }
        public string num_product_code { get; set; }
        public string num_report_type_code { get; set; }
        public string num_html_template_no { get; set; }
        public string txt_source_path { get; set; }
        public string txt_destination_path { get; set; }
        public string dat_start_date { get; set; }
        public string dat_end_date { get; set; }
        public string txt_policy_shedule { get; set; }
        public string txt_vehicle_class_code { get; set; }
        public string txt_risk_variant { get; set; }
        public string txt_user_id { get; set; }
        public string dat_insert_date { get; set; }

    }
}