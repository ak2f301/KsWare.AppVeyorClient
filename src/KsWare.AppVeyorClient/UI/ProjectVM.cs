﻿using KsWare.AppVeyorClient.Api.Contracts;
using KsWare.Presentation.Core.Providers;
using KsWare.Presentation.ViewModelFramework;

namespace KsWare.AppVeyorClient.UI {

	public class ProjectVM : DataVM<ProjectData> {

		public ProjectVM() {
			RegisterChildren(() => this);
		}

		protected override void OnDataChanged(DataChangedEventArgs e) {
			base.OnDataChanged(e);

			OnPropertyChanged(nameof(DisplayName));
		}

		public string DisplayName => Data?.Name;
	}

}