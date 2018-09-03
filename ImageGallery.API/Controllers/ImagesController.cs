using AutoMapper;
using ImageGallery.API.Helpers;
using ImageGallery.API.Services;
using ImageGallery.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageGallery.API.Controllers
{
    [Route("api/images")]
    [Authorize]
    public class ImagesController : Controller
    {
        private readonly IGalleryRepository _galleryRepository;
        private readonly IHostingEnvironment _hostingEnvironment;

        public ImagesController(IGalleryRepository galleryRepository,
            IHostingEnvironment hostingEnvironment)
        {
            _galleryRepository = galleryRepository;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet()]
        public IActionResult GetImages()
        {
            var ownerId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;

            var imagesFromRepo = _galleryRepository.GetImages(ownerId);

            var imagesToReturn = Mapper.Map<IEnumerable<Model.Image>>(imagesFromRepo);

            return Ok(imagesToReturn);
        }

        [HttpGet("{id}", Name = "GetImage")]
        [Authorize(Policy = "MustOwnImage")]
        public IActionResult GetImage(Guid id)
        {          
            var imageFromRepo = _galleryRepository.GetImage(id);

            if (imageFromRepo == null)
            {
                return NotFound();
            }

            var imageToReturn = Mapper.Map<Model.Image>(imageFromRepo);

            return Ok(imageToReturn);
        }

        [HttpPost()]
        [Authorize(Roles = "PayingUser")]
        public IActionResult CreateImage([FromBody] ImageForCreation imageForCreation)
        {
            if (imageForCreation == null)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            var imageEntity = Mapper.Map<Entities.Image>(imageForCreation);

            var webRootPath = _hostingEnvironment.WebRootPath;

            string fileName = Guid.NewGuid().ToString() + ".jpg";
            
            var filePath = Path.Combine($"{webRootPath}/images/{fileName}");

            System.IO.File.WriteAllBytes(filePath, imageForCreation.Bytes);

            imageEntity.FileName = fileName;

            var ownerId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            imageEntity.OwnerId = ownerId;

            _galleryRepository.AddImage(imageEntity);

            if (!_galleryRepository.Save())
            {
                throw new Exception($"Adding an image failed on save.");
            }

            var imageToReturn = Mapper.Map<Image>(imageEntity);

            return CreatedAtRoute("GetImage",
                new { id = imageToReturn.Id },
                imageToReturn);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "MustOwnImage")]
        public IActionResult DeleteImage(Guid id)
        {
            
            var imageFromRepo = _galleryRepository.GetImage(id);

            if (imageFromRepo == null)
            {
                return NotFound();
            }

            _galleryRepository.DeleteImage(imageFromRepo);

            if (!_galleryRepository.Save())
            {
                throw new Exception($"Deleting image with {id} failed on save.");
            }

            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "MustOwnImage")]
        public IActionResult UpdateImage(Guid id, 
            [FromBody] ImageForUpdate imageForUpdate)
        {
           
            if (imageForUpdate == null)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            var imageFromRepo = _galleryRepository.GetImage(id);
            if (imageFromRepo == null)
            {
                return NotFound();
            }

            Mapper.Map(imageForUpdate, imageFromRepo);

            _galleryRepository.UpdateImage(imageFromRepo);

            if (!_galleryRepository.Save())
            {
                throw new Exception($"Updating image with {id} failed on save.");
            }

            return NoContent();
        }
    }
}