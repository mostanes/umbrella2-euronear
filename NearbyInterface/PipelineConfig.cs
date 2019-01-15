using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class PipelineConfig : Form
	{
		public PipelineConfig()
		{
			InitializeComponent();
			propertyGrid1.SelectedObject = new StandardPipeline();
		}

		private void PipelineConfig_Load(object sender, EventArgs e)
		{
			StandardPipeline sp = new StandardPipeline();
			propertyGrid1.SelectedObject = sp;
		}
	}
}
