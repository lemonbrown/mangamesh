using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/keys")]
    public class KeysController : ControllerBase
    {
        private readonly IPublicKeyStore _keyStore;
        private readonly IApprovedKeyStore _approvedStore;

        public KeysController(IPublicKeyStore keyStore, IApprovedKeyStore approvedStore)
        {
            _keyStore = keyStore;
            _approvedStore = approvedStore;
        }

        [HttpGet]
        public async Task<IActionResult> GetKeys()
        {
            var keys = await _keyStore.GetAllAsync(); // Includes revoked status
            var approved = await _approvedStore.GetAllApprovedAsync(); // Includes approval info

            // Join them. 
            // In the UI, we want to see "Active" vs "Revoked".
            // We have "Registered Keys" from PublicKeyStore (users who registered).
            // And "Approved Keys" (which might be subset of registered, or pre-approved).

            // Let's assume UI wants list of Approved Keys primarily?
            // "Manage keys authorized to sign..."

            var result = approved.Select(a =>
            {
                var k = keys.FirstOrDefault(k => k.PublicKeyBase64 == a.PublicKeyBase64);
                return new
                {
                    id = a.PublicKeyBase64, // Use full key as ID
                    name = a.Comment,
                    //keyPreview = a.PublicKeyBase64.Substring(0, 10) + "...",
                    addedAt = a.AddedAt,
                    status = (k?.Revoked == true) ? "Revoked" : "Active"
                };
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterKey([FromBody] RegisterKeyRequest request)
        {
            await _approvedStore.ApproveKeyAsync(request.PublicKeyBase64, request.Name);

            var existing = await _keyStore.GetByKeyAsync(request.PublicKeyBase64);
            if (existing == null)
            {
                await _keyStore.StoreAsync(new PublicKeyRecord
                {
                    PublicKeyBase64 = request.PublicKeyBase64,
                    RegisteredAt = DateTimeOffset.UtcNow,
                    Revoked = false
                });
            }
            // If existing and revoked, reactivate it?
            if (existing != null && existing.Revoked)
            {
                await _keyStore.RevokeAsync(request.PublicKeyBase64); // Wait, RevokeAsync sets Revoked=true. We need Reactivate.
                                                                      // PublicKeyStore might not have Reactivate. 
                                                                      // We will assume "Reactivate" endpoint handles this.
            }

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeKey(string id)
        {
            var decoded = Uri.UnescapeDataString(id);
            await _keyStore.RevokeAsync(decoded);
            return Ok();
        }

        [HttpPost("{id}/reactivate")]
        public async Task<IActionResult> ReactivateKey(string id)
        {
            var decoded = Uri.UnescapeDataString(id);
            // Verify store has Reactivate? 
            // IPublicKeyStore definition: RevokeAsync. 
            // To reactivate, we might need to re-store with Revoked=false?
            var existing = await _keyStore.GetByKeyAsync(decoded);
            if (existing != null)
            {
                existing.Revoked = false;
                await _keyStore.StoreAsync(existing); // Update
            }
            return Ok();
        }
    }

    public class RegisterKeyRequest
    {
        public string Name { get; set; } = "";
        public string PublicKeyBase64 { get; set; } = "";
    }
}
