﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Acr.XamForms.Mobile;
using Acr.XamForms.Mobile.IO;
using Acr.XamForms.SignaturePad;
using Acr.XamForms.UserDialogs;
using Acr.XamForms.ViewModels;
using Samples.Models;
using Xamarin.Forms;


namespace Samples.ViewModels {

	public class SignatureListViewModel : ViewModel {

		private const string FILE_FORMAT = "{0:dd-MM-yyyy_hh-mm-ss_tt}.jpg";
		private readonly ISignatureService signatureService;
		private readonly IFileSystem fileSystem;
		private readonly IUserDialogService dialogs;


		public SignatureListViewModel(ISignatureService signatureService, IUserDialogService dialogs, IFileSystem fileSystem) {
			this.signatureService = signatureService;
			this.dialogs = dialogs;
			this.fileSystem = fileSystem;

			this.Create = new Command(async () => await this.OnCreate());
			this.List = new ObservableList<Signature>();
		}


		public override void OnAppearing() {  
			this.List.Clear();

			var signatures = this.fileSystem
				.AppData
				.Files
				.Select(x => new Signature {
					FileName = x.Name,
					FilePath = x.FullName,
					FileSize = x.Length
				})
				.ToList();

			this.List.AddRange(signatures);
			this.NoData = !this.List.Any();
		}


		public ObservableList<Signature> List { get; private set; }

		private bool noData;
		public bool NoData {
			get { return this.noData; }
			set { this.SetProperty(ref this.noData, value); }
		}


		public ICommand Create { get; private set; }


		private async Task OnCreate() {
			var result = await this.signatureService.Request();

			if (result.Cancelled)
				this.dialogs.Alert("Cancelled Signature");

			else {
				var fileName = String.Format(FILE_FORMAT, DateTime.Now);
				IFile file = null;
                using (var stream = result.GetStream()) {
					file = this.fileSystem.Temp.CreateFile(fileName);
					using (var fs = file.OpenWrite())
						stream.CopyTo(fs);
                }

				this.List.Add(new Signature {
					FilePath = file.FullName,
					FileName = file.Name,
					FileSize = file.Length
				});
				this.dialogs.Alert(String.Format("Draw Points: {0}", result.Points.Count()));
				this.NoData = !this.List.Any();
			}
		}


		private Command<Signature> selectCmd;
		public Command<Signature> Select {
			get {
				this.selectCmd = this.selectCmd ?? new Command<Signature>(s => 
					this.dialogs.ActionSheet(new ActionSheetConfig()
						.Add("View", () => {
                            try {
							    Device.OpenUri(new Uri("file://" + s.FilePath));
                            }
                            catch {
                                this.dialogs.Alert("Cannot open file");
                            }
						})
						.Add("Delete", async () => {
							var r = await this.dialogs.ConfirmAsync(String.Format("Are you sure you want to delete {0}", s.FileName));
							if (!r)
								return;

							var file = this.fileSystem.GetFile(s.FilePath);
							file.Delete();
							this.List.Remove(s);
							this.NoData = !this.List.Any();
						})
						.Add("Cancel")
					)
				);
				return this.selectCmd;
			}
		}
	}
}

