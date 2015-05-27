using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DTMScriptManager.Models
{
    public class wsdl_report_conditional_mst
    {
        public string num_line_of_business_code { get; set; }
        public string num_product_code { get; set; }
        public string txt_service_type { get; set; }
        public string txt_wsdl_type { get; set; }
        public string num_conf_wsdl_template_no { get; set; }
        public string num_report_type_code { get; set; }
        public string num_conf_property_no { get; set; }
        public string txt_conf_property_name { get; set; }
        public string num_serial_no { get; set; }
        public string txt_original_value { get; set; }
        public string txt_replaced_value { get; set; }
        public string txt_user_id { get; set; }
        public string dat_insert_date { get; set; }
    }
}