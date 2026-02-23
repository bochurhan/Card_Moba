-- ============================================
-- Card_Moba 数据库初始化脚本
-- MySQL 8.0
-- ============================================

CREATE DATABASE IF NOT EXISTS `card_moba` DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
USE `card_moba`;

-- 玩家账号表
CREATE TABLE `player_account` (
  `player_id` bigint NOT NULL AUTO_INCREMENT COMMENT '玩家唯一ID',
  `account` varchar(64) NOT NULL COMMENT '账号',
  `password_hash` varchar(128) NOT NULL COMMENT '加盐哈希密码',
  `nickname` varchar(32) NOT NULL COMMENT '玩家昵称',
  `level` int NOT NULL DEFAULT '1' COMMENT '玩家等级',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`player_id`),
  UNIQUE KEY `uk_account` (`account`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家账号表';

-- 玩家卡组表
CREATE TABLE `player_deck` (
  `deck_id` bigint NOT NULL AUTO_INCREMENT COMMENT '卡组唯一ID',
  `player_id` bigint NOT NULL COMMENT '玩家ID',
  `hero_id` int NOT NULL COMMENT '职业ID',
  `deck_name` varchar(32) NOT NULL COMMENT '卡组名称',
  `card_config` json NOT NULL COMMENT '卡牌配置JSON',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`deck_id`),
  KEY `idx_player_id` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家卡组表';

-- 对局记录表
CREATE TABLE `battle_record` (
  `battle_id` bigint NOT NULL AUTO_INCREMENT COMMENT '对局唯一ID',
  `battle_time` datetime NOT NULL COMMENT '对局时间',
  `win_team` tinyint NOT NULL COMMENT '获胜队伍',
  `player_ids` json NOT NULL COMMENT '对局玩家ID列表',
  `battle_duration` int NOT NULL COMMENT '对局时长(秒)',
  `battle_round` int NOT NULL COMMENT '对局回合数',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`battle_id`),
  KEY `idx_battle_time` (`battle_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='对局记录表';
