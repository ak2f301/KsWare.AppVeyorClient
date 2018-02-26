﻿using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using JetBrains.Annotations;
using KsWare.AppVeyorClient.Api;
using KsWare.AppVeyorClient.Shared;
using KsWare.Presentation.ViewModelFramework;

namespace KsWare.AppVeyorClient.UI {

	public class AppVM:ApplicationVM {

		public AppVM() {
			RegisterChildren(()=>this);
			StartupUri = typeof(MainWindowVM);
		}

		internal static Client Client { get; } = new Client("");
		public static FileStore FileStore { get; private set; }

		internal static void StoreToken([NotNull]SecureString secureToken) {
			if (secureToken == null) throw new ArgumentNullException(nameof(secureToken));

			byte[] plaintext = Encoding.UTF8.GetBytes(
				System.Runtime.InteropServices.Marshal.PtrToStringAuto(
					System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secureToken)));

//			byte[] plaintext = Encoding.UTF8.GetBytes(unsecureToken);

			// Generate additional entropy (will be used as the Initialization vector)
			byte[] entropy = new byte[20];
			using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
				rng.GetBytes(entropy);


			byte[] ciphertext = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);


			int chk = 0;
			foreach (var b in ciphertext)
				chk += b;
			Debug.WriteLine(chk);


			//			IsolatedStorageFile isoStore =
			//				IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Machine, null, null);
			//			using (var isoStream = new IsolatedStorageFileStream("Token", FileMode.Create, isoStore)) {
			using (var isoStream = File.Create("Token")) {

				using (var writer = new BinaryWriter(isoStream)) {
					writer.Write(entropy);
					writer.Write(ciphertext.Length);
					writer.Write(ciphertext);
					writer.Flush();
				}
			}
			Client.SetToken(secureToken);
		}

		internal static void LoadToken() {

//			var isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User|IsolatedStorageScope.Machine, null, null);
//			if (!isoStore.FileExists("Token")) return;
			if (!File.Exists("Token")) return;

			byte[] entropy;
			byte[] ciphertext;
			//			using (var isoStream = new IsolatedStorageFileStream("Token", FileMode.Open, isoStore)
			using (var isoStream = File.OpenRead("Token")
			) {

				using (var reader = new BinaryReader(isoStream)) {
					entropy = reader.ReadBytes(20);
					ciphertext = reader.ReadBytes(reader.ReadInt32());
				}
			}

			byte[] plaintext;
			try {
				plaintext = ProtectedData.Unprotect(ciphertext, entropy, DataProtectionScope.CurrentUser);
			}
			catch (Exception ex) {
				MessageBox.Show("Error", "Can not restore token. \n\n" + ex.Message);
				return;
			}
			var ss = new SecureString();
			foreach (var c in Encoding.UTF8.GetString(plaintext)) ss.AppendChar(c);
			Client.SetToken(ss);
		}

		internal static void InitFileStore() {
			FileStore=FileStore.Instance=new FileStore(Path.Combine(Directory.GetCurrentDirectory(),"Cache"));
		}
	}
}
