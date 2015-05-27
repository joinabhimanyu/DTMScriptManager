using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DTMScriptManager.Models
{
    public class wsdl_report_risk_mapping
    {
        public string num_line_of_business_code { get; set; }
        public string num_product_code { get; set; }
        public string txt_wsdl_type { get; set; }
        public string txt_service_type { get; set; }
        public string num_report_type_code { get; set; }
        public string num_html_template_no { get; set; }
        public string num_serial_no { get; set; }
        public string num_conf_property_no { get; set; }
        public string txt_conf_property_name { get; set; }
        public string txt_yn_has_child { get; set; }
        public string num_conf_parent_property_no { get; set; }
        public string txt_conf_class_name { get; set; }
        public string txt_actual_page_name { get; set; }
        public string txt_conf_property_data_type { get; set; }
        public string num_label_code { get; set; }
        public string txt_label_name { get; set; }
        public string txt_conf_parent_property_name { get; set; }
        public string txt_mapping_type { get; set; }
        public string txt_is_word { get; set; }
        public string txt_zero_replace { get; set; }
        public string txt_blank_replace { get; set; }
        public string txt_is_old_value { get; set; }
        public string yn_risked_mapped_value { get; set; }
        public string txt_risk_component { get; set; }
        public string yn_cover_mapped_value { get; set; }
        public string txt_cover_component { get; set; }
        public string yn_ld_mapped_value { get; set; }
        public string txt_ld_component { get; set; }
        public string txt_risk_group_property { get; set; }
        public string txt_cover_group_property { get; set; }
        public string txt_ld_group_property { get; set; }
        public string txt_rcld_component { get; set; }
        public string txt_user_id { get; set; }
        public string dat_insert_date { get; set; }
    }
}