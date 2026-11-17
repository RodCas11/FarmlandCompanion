using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed partial class BuildViewModel
{
 bool readOnlyPreview;public string PreviewAuthor {get;private set;}="Autor não informado";
 public string PreviewAdminNotes {get;private set;}="";public bool HasAdminNotes=>!string.IsNullOrWhiteSpace(PreviewAdminNotes);
 public string BuildDetails=>SelectedClass.Name+" · "+Stage+" Game · "+Purpose;
 BuildViewModel? communityPreview,reviewPreview;CommunityBuild? previewSource;
 public BuildViewModel? CommunityPreview {get=>communityPreview;private set{communityPreview?.Dispose();communityPreview=value;Changed();Changed(nameof(HasCommunityPreview));}}
 public bool HasCommunityPreview=>CommunityPreview!=null;
 public BuildViewModel? ReviewPreview {get=>reviewPreview;private set{reviewPreview?.Dispose();reviewPreview=value;Changed();}}
 sealed class OfflinePreviewGateway:IBuildCommunityGateway {public bool Configured=>false;public Task<BuildCommunitySnapshot> FetchAsync(string c,string s,string p,CancellationToken ct)=>throw new NotSupportedException();public Task SubmitAsync(PlayerBuild b,CancellationToken ct)=>throw new NotSupportedException();}
 BuildViewModel Preview(PlayerBuild build){var result=new BuildViewModel(store,new OfflinePreviewGateway(),Equipment,Items){readOnlyPreview=true};try{result.Load(Clone(build));return result;}catch{result.Dispose();throw;}}
 public ICommand ViewBuildCommand=>new RelayCommand(async value=>{if(value is not CommunityBuild row||Busy)return;Busy=true;try{row=await CommunityContent(row);var view=Preview(row.Build);view.PreviewAuthor=BuildPublicName.Clean(row.AuthorName);view.PreviewAdminNotes=row.AdminNotes;CommunityPreview=view;previewSource=row;}catch{Tell("Não foi possível visualizar esta build. Os dados locais foram preservados.");}finally{Busy=false;}});
 public ICommand EditOwnedCommunityCommand=>new RelayCommand(async value=>{if(Busy||value is not CommunityBuild row||row.OwnerUid!=FirebaseUid||!IsSignedIn)return;Busy=true;try{row=await CommunityContent(row);OpenSaved(row.Build);}catch(Exception e){Tell(e.Message);}finally{Busy=false;}});
 public ICommand CloseBuildPreviewCommand=>new RelayCommand(_=>{CommunityPreview=null;previewSource=null;});
 public ICommand CopyBuildPreviewCommand=>new RelayCommand(_=>{if(previewSource==null)return;CopyCommunity(previewSource);CommunityPreview=null;previewSource=null;});
 void SetReviewPreview(){ReviewPreview=null;if(SelectedSubmission is {} row&&row.Issue.Length==0)try{ReviewPreview=Preview(row.Build);ReviewPreview.PreviewAuthor=BuildPublicName.Clean(row.Fields.TryGetValue("authorName",out var author)?author.GetProperty("stringValue").GetString():null);}catch{Tell("Não foi possível montar a visualização. Confira os detalhes da submissão.");}}
}
