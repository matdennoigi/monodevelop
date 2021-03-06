﻿//
// ReinstallPackageActionTests.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using ICSharpCode.PackageManagement;
using MonoDevelop.PackageManagement.Tests.Helpers;
using MonoDevelop.Projects;
using NuGet;
using NUnit.Framework;

namespace MonoDevelop.PackageManagement.Tests
{
	[TestFixture]
	public class ReinstallPackageActionTests
	{
		ReinstallPackageAction action;
		PackageManagementEvents packageManagementEvents;
		FakePackageManagementProject project;
		FakeFileRemover fileRemover;

		void CreateAction (string packageId = "MyPackage", string packageVersion = "1.2.3.4")
		{
			project = new FakePackageManagementProject ();
			project.AddFakeInstallOperation ();

			packageManagementEvents = new PackageManagementEvents ();

			fileRemover = new FakeFileRemover ();

			action = new ReinstallPackageAction (project, packageManagementEvents, fileRemover);
			action.PackageId = packageId;
			action.PackageVersion = new SemanticVersion (packageVersion);
		}

		FakePackage AddPackageToSourceRepository (string packageId, string packageVersion)
		{
			return project.FakeSourceRepository.AddFakePackageWithVersion (packageId, packageVersion);
		}

		[Test]
		public void Execute_PackageExistsInSourceRepository_PackageIsUninstalled ()
		{
			CreateAction ("MyPackage", "1.2.3.4");
			FakePackage package = AddPackageToSourceRepository ("MyPackage", "1.2.3.4");

			action.Execute ();

			Assert.IsTrue (project.FakeUninstallPackageAction.IsExecuted);
			Assert.AreEqual (package, project.FakeUninstallPackageAction.Package);
		}

		[Test]
		public void Execute_PackageExistsInSourceRepository_PackageIsInstalled ()
		{
			CreateAction ("MyPackage", "1.2.3.4");
			FakePackage package = AddPackageToSourceRepository ("MyPackage", "1.2.3.4");

			action.Execute ();

			Assert.IsTrue (project.LastInstallPackageCreated.IsExecuteCalled);
			Assert.AreEqual (package, project.LastInstallPackageCreated.Package);
		}

		[Test]
		public void Execute_PackageExistsInSourceRepository_PackageIsForcefullyRemovedSoItDoesNotFailIfOtherPackagesDependOnIt ()
		{
			CreateAction ("MyPackage", "1.2.3.4");
			AddPackageToSourceRepository ("MyPackage", "1.2.3.4");

			action.Execute ();

			Assert.IsTrue (project.FakeUninstallPackageAction.ForceRemove);
		}

		[Test]
		public void Execute_ReferenceHasLocalCopyFalseWhenUninstalled_ReferenceHasLocalCopyFalseAfterBeingReinstalled ()
		{
			CreateAction ("MyPackage", "1.2.3.4");
			FakePackage package = AddPackageToSourceRepository ("MyPackage", "1.2.3.4");
			var firstReferenceBeingAdded = new ProjectReference (ReferenceType.Assembly, "NewAssembly");
			var secondReferenceBeingAdded = new ProjectReference (ReferenceType.Assembly, "NUnit.Framework");
			project.FakeUninstallPackageAction.ExecuteAction = () => {
				var referenceBeingRemoved = new ProjectReference (ReferenceType.Assembly, "NUnit.Framework") {
					LocalCopy = false
				};
				packageManagementEvents.OnReferenceRemoving (referenceBeingRemoved);
			};
			bool installActionMaintainsLocalCopyReferences = false;
			project.InstallPackageExecuteAction = () => {
				installActionMaintainsLocalCopyReferences = project.LastInstallPackageCreated.PreserveLocalCopyReferences;
				packageManagementEvents.OnReferenceAdding (firstReferenceBeingAdded);
				packageManagementEvents.OnReferenceAdding (secondReferenceBeingAdded);
			};
			action.Execute ();

			Assert.IsTrue (firstReferenceBeingAdded.LocalCopy);
			Assert.IsFalse (secondReferenceBeingAdded.LocalCopy);
			Assert.IsFalse (installActionMaintainsLocalCopyReferences, "Should be false since the reinstall action will maintain the local copies");
		}

		[Test]
		public void Execute_PackageExistsInSourceRepository_PackageIsInstalledWithoutOpeningReadmeTxt ()
		{
			CreateAction ("MyPackage", "1.2.3.4");
			FakePackage package = AddPackageToSourceRepository ("MyPackage", "1.2.3.4");

			action.Execute ();

			Assert.IsTrue (project.LastInstallPackageCreated.IsExecuteCalled);
			Assert.IsFalse (project.LastInstallPackageCreated.OpenReadMeText);
		}

		[Test]
		public void Execute_PackagesConfigFileDeletedDuringUninstall_FileServicePackagesConfigFileDeletionIsCancelled ()
		{
			CreateAction ();
			action.Package = new FakePackage ("Test");
			string expectedFileName = @"d:\projects\MyProject\packages.config".ToNativePath ();
			bool? fileRemovedResult = null;
			project.UninstallPackageAction = (p, a) => {
				fileRemovedResult = packageManagementEvents.OnFileRemoving (expectedFileName);
			};
			project.CreateUninstallPackageActionFunc = () => {
				return new UninstallPackageAction (project, packageManagementEvents);
			};
			action.Execute ();

			Assert.AreEqual (expectedFileName, fileRemover.FileRemoved);
			Assert.IsFalse (fileRemovedResult.Value);
		}

		[Test]
		public void Execute_ScriptFileDeletedDuringUninstall_FileDeletionIsNotCancelled ()
		{
			CreateAction ();
			action.Package = new FakePackage ("Test");
			string fileName = @"d:\projects\MyProject\scripts\myscript.js".ToNativePath ();
			bool? fileRemovedResult = null;
			project.UninstallPackageAction = (p, a) => {
				fileRemovedResult = packageManagementEvents.OnFileRemoving (fileName);
			};
			project.CreateUninstallPackageActionFunc = () => {
				return new UninstallPackageAction (project, packageManagementEvents);
			};
			action.Execute ();

			Assert.IsTrue (fileRemovedResult.Value);
			Assert.IsNull (fileRemover.FileRemoved);
		}
	}
}

