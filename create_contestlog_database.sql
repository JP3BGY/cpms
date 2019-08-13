-- MySQL Workbench Forward Engineering

SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0;
SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;
SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- -----------------------------------------------------
-- Schema contest_log
-- -----------------------------------------------------

-- -----------------------------------------------------
-- Schema contest_log
-- -----------------------------------------------------
CREATE SCHEMA IF NOT EXISTS `contest_log` ;
USE `contest_log` ;

-- -----------------------------------------------------
-- Table `contest_log`.`contest_server`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`contest_server` (
  `contestServerId` INT NOT NULL AUTO_INCREMENT,
  `contestServerName` VARCHAR(45) NOT NULL,
  `contestServerUrl` VARCHAR(512) NOT NULL,
  PRIMARY KEY (`contestServerId`),
  UNIQUE INDEX `contestid_UNIQUE` (`contestServerId` ) ,
  UNIQUE INDEX `contestName_UNIQUE` (`contestServerName` ) ,
  UNIQUE INDEX `contestUrl_UNIQUE` (`contestServerUrl` ) )
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`contest`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`contest` (
  `contestId` INT NOT NULL AUTO_INCREMENT,
  `contestName` VARCHAR(256) NOT NULL,
  `contest_server_contestServerId` INT NOT NULL,
  `contestServerContestId` VARCHAR(256) NOT NULL,
  PRIMARY KEY (`contestId`),
  INDEX `fk_contest_contest_server1_idx` (`contest_server_contestServerId` ) ,
  UNIQUE INDEX `contestId_UNIQUE` (`contestId` ) ,
  UNIQUE INDEX `contest_key` (`contest_server_contestServerId` , `contestServerContestId` ) ,
  INDEX `contestName_idx` (`contestName` ) ,
  CONSTRAINT `fk_contest_contest_server1`
    FOREIGN KEY (`contest_server_contestServerId`)
    REFERENCES `contest_log`.`contest_server` (`contestServerId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`problem`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`problem` (
  `problemId` INT NOT NULL AUTO_INCREMENT,
  `problemName` VARCHAR(256) NOT NULL,
  `contestServerProblemId` VARCHAR(45) NOT NULL,
  `contest_contestId` INT NOT NULL,
  PRIMARY KEY (`problemId`),
  UNIQUE INDEX `problemId_UNIQUE` (`problemId` ) ,
  INDEX `fk_problem_contest1_idx` (`contest_contestId` ) ,
  UNIQUE INDEX `problem_unique` (`contest_contestId` , `contestServerProblemId` ) ,
  CONSTRAINT `fk_problem_contest1`
    FOREIGN KEY (`contest_contestId`)
    REFERENCES `contest_log`.`contest` (`contestId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`problem_difficulty`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`problem_difficulty` (
  `problem_problemId` INT NOT NULL,
  `problemDifficulty` DOUBLE NOT NULL,
  PRIMARY KEY (`problem_problemId`),
  UNIQUE INDEX `problem_problemId_UNIQUE` (`problem_problemId` ) ,
  INDEX `difficulty` (`problemDifficulty` ) ,
  CONSTRAINT `fk_problem_difficulty_problem1`
    FOREIGN KEY (`problem_problemId`)
    REFERENCES `contest_log`.`problem` (`problemId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`problem_tag`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`problem_tag` (
  `tag` VARCHAR(256) NOT NULL,
  `problem_problemId` INT NOT NULL,
  `is_created` TINYINT NOT NULL,
  INDEX `fk_problem_tag_problem1_idx` (`problem_problemId` ) ,
  INDEX `is_created_index` (`is_created` ) ,
  INDEX `tag_index` (`tag` ) ,
  PRIMARY KEY (`tag`, `problem_problemId`),
  CONSTRAINT `fk_problem_tag_problem1`
    FOREIGN KEY (`problem_problemId`)
    REFERENCES `contest_log`.`problem` (`problemId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`contest_participants`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`contest_participants` (
  `contest_contestId` INT NOT NULL,
  `rating` INT NOT NULL,
  INDEX `fk_contest_participants_contest1_idx` (`contest_contestId` ) ,
  INDEX `contest_participants_index` (`contest_contestId` , `rating` ) ,
  CONSTRAINT `fk_contest_participants_contest1`
    FOREIGN KEY (`contest_contestId`)
    REFERENCES `contest_log`.`contest` (`contestId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`contest_users`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`contest_users` (
  `userId` INT NOT NULL AUTO_INCREMENT,
  `contestUserId` VARCHAR(256) NOT NULL,
  `contest_server_contestServerId` INT NOT NULL,
  PRIMARY KEY (`userId`),
  UNIQUE INDEX `userId_UNIQUE` (`userId` ) ,
  INDEX `fk_contest_users_contest_server1_idx` (`contest_server_contestServerId` ) ,
  INDEX `contest_users_UNIQUE` (`contestUserId` , `contest_server_contestServerId` ) ,
  CONSTRAINT `fk_contest_users_contest_server1`
    FOREIGN KEY (`contest_server_contestServerId`)
    REFERENCES `contest_log`.`contest_server` (`contestServerId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`problem_solver_in_contest`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`problem_solver_in_contest` (
  `problem_problemId` INT NOT NULL,
  `rating` INT NOT NULL,
  `contest_users_userId` INT NOT NULL,
  INDEX `fk_problem_solver_in_contest_problem1_idx` (`problem_problemId` ) ,
  INDEX `fk_problem_solver_in_contest_contest_users1_idx` (`contest_users_userId` ) ,
  CONSTRAINT `fk_problem_solver_in_contest_problem1`
    FOREIGN KEY (`problem_problemId`)
    REFERENCES `contest_log`.`problem` (`problemId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION,
  CONSTRAINT `fk_problem_solver_in_contest_contest_users1`
    FOREIGN KEY (`contest_users_userId`)
    REFERENCES `contest_log`.`contest_users` (`userId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`problem_submissions`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`problem_submissions` (
  `problem_problemId` INT NOT NULL,
  `contest_users_userId` INT NOT NULL,
  `submission_time` BIGINT NOT NULL,
  `submission_status` VARCHAR(5) NOT NULL,
  `contestServerSubmissonId` BIGINT NOT NULL,
  INDEX `fk_problem_solver_problem1_idx` (`problem_problemId` ) ,
  INDEX `fk_problem_solver_contest_users1_idx` (`contest_users_userId` ) ,
  PRIMARY KEY (`problem_problemId`, `contest_users_userId`, `contestServerSubmissonId`),
  CONSTRAINT `fk_problem_solver_problem1`
    FOREIGN KEY (`problem_problemId`)
    REFERENCES `contest_log`.`problem` (`problemId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION,
  CONSTRAINT `fk_problem_solver_contest_users1`
    FOREIGN KEY (`contest_users_userId`)
    REFERENCES `contest_log`.`contest_users` (`userId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


-- -----------------------------------------------------
-- Table `contest_log`.`watching_user`
-- -----------------------------------------------------
CREATE TABLE IF NOT EXISTS `contest_log`.`watching_user` (
  `contest_users_userId` INT NOT NULL,
  PRIMARY KEY (`contest_users_userId`),
  CONSTRAINT `fk_watching_user_contest_users1`
    FOREIGN KEY (`contest_users_userId`)
    REFERENCES `contest_log`.`contest_users` (`userId`)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION)
ENGINE = InnoDB;


SET SQL_MODE=@OLD_SQL_MODE;
SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS;
SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS;
