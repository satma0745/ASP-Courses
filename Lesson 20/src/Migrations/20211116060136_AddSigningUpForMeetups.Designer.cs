﻿// <auto-generated />
using System;
using Meets.WebApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Meets.WebApi.Migrations
{
    using Meets.WebApi;
    using Meets.WebApi.Persistence;

    [DbContext(typeof(DatabaseContext))]
    [Migration("20211116060136_AddSigningUpForMeetups")]
    partial class AddSigningUpForMeetups
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Meets.WebApi.Entities.MeetupEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("Duration")
                        .HasColumnType("integer")
                        .HasColumnName("duration");

                    b.Property<string>("Place")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("place");

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("topic");

                    b.HasKey("Id")
                        .HasName("pk_meetups");

                    b.ToTable("meetups", (string)null);
                });

            modelBuilder.Entity("Meets.WebApi.Entities.RefreshTokenEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTime>("ExpirationTime")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expiration_time");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_refresh_tokens");

                    b.ToTable("refresh_tokens", (string)null);
                });

            modelBuilder.Entity("Meets.WebApi.Entities.UserEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("display_name");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.HasIndex("Username")
                        .IsUnique()
                        .HasDatabaseName("ix_users_username");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("meetups_signed_up_users", b =>
                {
                    b.Property<Guid>("user_id")
                        .HasColumnType("uuid");

                    b.Property<Guid>("meetup_id")
                        .HasColumnType("uuid");

                    b.HasKey("user_id", "meetup_id")
                        .HasName("pk_meetups_signed_up_users");

                    b.HasIndex("meetup_id")
                        .HasDatabaseName("ix_meetups_signed_up_users_meetup_id");

                    b.ToTable("meetups_signed_up_users");
                });

            modelBuilder.Entity("meetups_signed_up_users", b =>
                {
                    b.HasOne("Meets.WebApi.Entities.MeetupEntity", null)
                        .WithMany()
                        .HasForeignKey("meetup_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_users_signed_up_meetups");

                    b.HasOne("Meets.WebApi.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_meetups_signed_up_users");
                });
#pragma warning restore 612, 618
        }
    }
}
